using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace StockPriceRecord
{
    public partial class Form1 : Form
    {
        public class StockPrice
        {
            public string Symbol { get; set; }
            public DateTime Date { get; set; }
            public decimal Price { get; set; }

            public StockPrice(string symbol, DateTime date, decimal price)
            {
                Symbol = symbol;
                Date = date;
                Price = price;
            }
        }

        private readonly List<StockPrice> _data = new List<StockPrice>();

        public Form1()
        {
            InitializeComponent();
            InitUi();
            InitChart();
            RefreshGridAndSymbols();
        }

        private void InitUi()
        {
            dtpDate.Format = DateTimePickerFormat.Short;

            cmbRange.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRange.Items.AddRange(new object[] { "7天", "30天", "90天", "1年", "自訂" });
            cmbRange.SelectedIndex = 1;

            dtpFrom.Format = DateTimePickerFormat.Short;
            dtpTo.Format = DateTimePickerFormat.Short;

            cmbSymbol.DropDownStyle = ComboBoxStyle.DropDownList;
        }

        private void InitChart()
        {
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();

            var area = new ChartArea("Main");
            area.AxisX.LabelStyle.Format = "yyyy-MM-dd";
            area.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chart1.ChartAreas.Add(area);

            chart1.Legends.Clear();
            chart1.Legends.Add(new Legend("Legend"));
        }

        // === 手動新增 ===
        private void btnAdd_Click(object sender, EventArgs e)
        {
            var symbol = (txtSymbol.Text ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(symbol))
            {
                MessageBox.Show("請輸入股票代號(Symbol)。");
                return;
            }

            var date = dtpDate.Value.Date;
            var price = nudPrice.Value;

            _data.Add(new StockPrice(symbol, date, price));
            RefreshGridAndSymbols();
        }

        // === 匯入 CSV ===
        private void btnImport_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "選擇要匯入的 CSV"
            })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    var imported = ImportCsv(ofd.FileName);
                    _data.AddRange(imported);
                    RefreshGridAndSymbols();
                    MessageBox.Show($"匯入完成：{imported.Count} 筆");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("匯入失敗：" + ex.Message);
                }
            }
        }

        private List<StockPrice> ImportCsv(string path)
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length <= 1) return new List<StockPrice>();

            // 期待表頭：Symbol,Date,Price
            var result = new List<StockPrice>();
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                var symbol = parts[0].Trim().ToUpperInvariant();
                var dateStr = parts[1].Trim();
                var priceStr = parts[2].Trim();

                DateTime date;
                var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy/M/d" };

                if (!DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
                {
                    throw new FormatException($"第 {i + 1} 行 Date 格式錯誤（支援 yyyy-MM-dd 或 yyyy/MM/dd）：{dateStr}");
                }


                decimal price;
                if (!decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                {
                    throw new FormatException($"第 {i + 1} 行 Price 格式錯誤：{priceStr}");
                }

                

                result.Add(new StockPrice(symbol, date.Date, price));
            }
            return result;
        }

        // === 畫圖 ===
        private void btnPlot_Click(object sender, EventArgs e)
        {
            MessageBox.Show("btnPlot_Click 有進來");

            if (cmbSymbol.SelectedItem == null)
            {
                MessageBox.Show("請先選股票");
                return;
            }

            var symbol = cmbSymbol.SelectedItem.ToString();
            var from = dtpFrom.Value.Date;
            var to = dtpTo.Value.Date;

            var points = _data
                .Where(x => x.Symbol == symbol && x.Date >= from && x.Date <= to)
                .OrderBy(x => x.Date)
                .ToList();

            MessageBox.Show($"符合條件筆數: {points.Count}\n範圍: {from:yyyy-MM-dd} ~ {to:yyyy-MM-dd}");

            Plot(symbol, points);
        }


        private (DateTime from, DateTime to) GetRange()
        {
            var to = DateTime.Today;
            var from = to.AddDays(-30);

            var selected = cmbRange.SelectedItem?.ToString() ?? "30天";
            if (selected == "7天") from = to.AddDays(-7);
            else if (selected == "30天") from = to.AddDays(-30);
            else if (selected == "90天") from = to.AddDays(-90);
            else if (selected == "1年") from = to.AddYears(-1);
            else if (selected == "自訂")
            {
                from = dtpFrom.Value.Date;
                to = dtpTo.Value.Date;
                if (from > to) (from, to) = (to, from);
            }

            return (from.Date, to.Date);
        }

        private void Plot(string symbol, List<StockPrice> points)
        {
            chart1.Series.Clear();

            var s = new Series(symbol)
            {
                ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.Date,
                BorderWidth = 2,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 5
            };

            foreach (var p in points)
            {
                s.Points.AddXY(p.Date, p.Price);
            }

            chart1.Series.Add(s);
            chart1.ChartAreas["Main"].RecalculateAxesScale();

            if (points.Count == 0)
            {
                MessageBox.Show("該期限內沒有資料可畫圖。");
            }
        }

        // === Grid + Symbol 下拉更新 ===
        private void RefreshGridAndSymbols()
        {
            grid.AutoGenerateColumns = true;
            grid.DataSource = _data
                .OrderByDescending(x => x.Date)
                .ThenBy(x => x.Symbol)
                .ToList();

            var symbols = _data.Select(x => x.Symbol).Distinct().OrderBy(x => x).ToList();
            var current = cmbSymbol.SelectedItem?.ToString();

            cmbSymbol.Items.Clear();
            foreach (var s in symbols) cmbSymbol.Items.Add(s);

            if (current != null && symbols.Contains(current)) cmbSymbol.SelectedItem = current;
            else if (symbols.Count > 0) cmbSymbol.SelectedIndex = 0;
        }

        private void chart1_Click(object sender, EventArgs e)
        {

        }
    }
}
