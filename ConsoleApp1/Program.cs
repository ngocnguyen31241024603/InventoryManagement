using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace InventoryConsole
{
    enum Category { ThucPham = 0, DienTu = 1, QuanAo = 2, GiaDung = 3, Khac = 4 }

    class Product
    {
        public string Code;
        public string Name;
        public Category Type;
        public int Quantity;
        public decimal CostPrice;
        public decimal SellPrice;

        public decimal InventoryValue() { return Quantity * SellPrice; }
        public decimal ProfitEstimate() { return (SellPrice - CostPrice) * Quantity; }

        public static Product Create(string code, string name, Category type, int qty, decimal cost, decimal sell)
        {
            Product p = new Product();
            p.Code = code; p.Name = name; p.Type = type; p.Quantity = qty; p.CostPrice = cost; p.SellPrice = sell;
            return p;
        }

        public string ToCsv()
        {
            return string.Join(",",
                Code,
                Name,
                ((int)Type).ToString(CultureInfo.InvariantCulture),
                Quantity.ToString(CultureInfo.InvariantCulture),
                CostPrice.ToString(CultureInfo.InvariantCulture),
                SellPrice.ToString(CultureInfo.InvariantCulture));
        }

        public static bool TryParseCsv(string line, out Product p)
        {
            p = null;
            if (string.IsNullOrWhiteSpace(line)) return false;
            string[] parts = line.Split(',');
            if (parts.Length < 6) return false;

            string code = parts[0].Trim();
            string name = parts[1].Trim();

            int typeInt; int qty; decimal cost; decimal sell;
            if (!int.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out typeInt)) return false;
            if (!int.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out qty)) return false;
            if (!decimal.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out cost)) return false;
            if (!decimal.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out sell)) return false;

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) return false;
            if (typeInt < 0 || typeInt > 4) typeInt = 4;
            if (qty < 0 || cost < 0 || sell < 0) return false;

            p = new Product { Code = code, Name = name, Type = (Category)typeInt, Quantity = qty, CostPrice = cost, SellPrice = sell };
            return true;
        }
    }

    class ProductRepository
    {
        public const int InitialCapacity = 8;
        public const int RestockThreshold = 5; // NGƯỠNG CỐ ĐỊNH = 5
        public static readonly string DefaultDataFile = "data.csv";
        public static readonly string LogFile = "log.txt";

        private Product[] _items;
        private int _count;
        private int[,] _byCategoryByMonth = new int[Enum.GetValues(typeof(Category)).Length, 12];

        public ProductRepository(int capacity = InitialCapacity)
        {
            if (capacity < 1) capacity = InitialCapacity;
            _items = new Product[capacity];
            _count = 0;
        }

        public int Count { get { return _count; } }
        public Product this[int index] { get { return (index >= 0 && index < _count) ? _items[index] : null; } }

        private void Log(string action, Product p)
        {
            try
            {
                string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} | {1} | {2} | {3} | Qty={4} | Cost={5} | Sell={6}",
                    DateTime.Now, action, p != null ? p.Code : "", p != null ? p.Name : "",
                    p != null ? p.Quantity : 0, p != null ? p.CostPrice : 0, p != null ? p.SellPrice : 0);
                using (var sw = new StreamWriter(LogFile, true, Encoding.UTF8)) { sw.WriteLine(line); }
            }
            catch { }
        }

        private void EnsureCapacity()
        {
            if (_count < _items.Length) return;
            int newCap = _items.Length * 2;
            Product[] bigger = new Product[newCap];
            for (int i = 0; i < _count; i++) bigger[i] = _items[i];
            _items = bigger;
        }

        public bool TryGetIndexByCode(string code, out int index)
        {
            index = -1;
            for (int i = 0; i < _count; i++)
            {
                if (string.Equals(_items[i].Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    index = i; return true;
                }
            }
            return false;
        }

        public bool ExistsCode(string code)
        {
            int idx; return TryGetIndexByCode(code, out idx);
        }

        public bool Add(Product p, int month)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Code) || string.IsNullOrWhiteSpace(p.Name)) return false;
            if (p.Quantity < 0 || p.CostPrice < 0 || p.SellPrice < 0) return false;
            if (ExistsCode(p.Code)) return false;

            EnsureCapacity();
            _items[_count++] = p;

            int m = (month >= 1 && month <= 12) ? month : DateTime.Now.Month;
            _byCategoryByMonth[(int)p.Type, m - 1] += p.Quantity;

            Log("ADD", p);
            return true;
        }
        public bool Add(string code, string name, Category type, int qty, decimal cost, decimal sell, int month)
        {
            return Add(Product.Create(code, name, type, qty, cost, sell), month);
        }

        public bool Update(string code, string newName, Category? newType, int? newQty, decimal? newCost, decimal? newSell)
        {
            int idx;
            if (!TryGetIndexByCode(code, out idx)) return false;
            Product p = _items[idx];

            if (!string.IsNullOrWhiteSpace(newName)) p.Name = newName;
            if (newType.HasValue) p.Type = newType.Value;
            if (newQty.HasValue && newQty.Value >= 0) p.Quantity = newQty.Value;
            if (newCost.HasValue && newCost.Value >= 0) p.CostPrice = newCost.Value;
            if (newSell.HasValue && newSell.Value >= 0) p.SellPrice = newSell.Value;

            Log("UPDATE", p);
            return true;
        }

        public bool Remove(string code)
        {
            int idx;
            if (!TryGetIndexByCode(code, out idx)) return false;
            Product p = _items[idx];
            for (int i = idx; i < _count - 1; i++) _items[i] = _items[i + 1];
            _items[_count - 1] = null;
            _count--;
            Log("DELETE", p);
            return true;
        }

        public void PrintAll(CultureInfo vi)
        {
            Console.WriteLine("----- DANH SÁCH SẢN PHẨM -----");
            if (_count == 0) { Console.WriteLine("(Trống)"); return; }
            Console.WriteLine("Mã | Tên | Loại | SL | Giá nhập | Giá bán | Giá trị tồn | LN dự kiến");
            for (int i = 0; i < _count; i++)
            {
                Product p = _items[i];
                Console.WriteLine(
                    p.Code + " | " + p.Name + " | " + p.Type + " | " + p.Quantity
                    + " | " + p.CostPrice.ToString("N2", vi)
                    + " | " + p.SellPrice.ToString("N2", vi)
                    + " | " + p.InventoryValue().ToString("N2", vi)
                    + " | " + p.ProfitEstimate().ToString("N2", vi));
            }
        }

        public void LinearSearch(string keyword)
        {
            if (keyword == null) keyword = "";
            keyword = keyword.Trim();
            Console.WriteLine("Kết quả tìm: \"" + keyword + "\"");
            bool found = false;
            for (int i = 0; i < _count; i++)
            {
                Product p = _items[i];
                bool hit = (p.Code != null && p.Code.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (p.Name != null && p.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit)
                {
                    Console.WriteLine("- " + p.Code + " | " + p.Name + " | " + p.Type + " | SL: " + p.Quantity);
                    found = true;
                }
            }
            if (!found) Console.WriteLine("(Không tìm thấy)");
        }

        public enum SortField { Code = 0, Name = 1, Quantity = 2, SellPrice = 3 }

        public void BubbleSort(SortField field, bool ascending)
        {
            for (int i = 0; i < _count - 1; i++)
            {
                for (int j = 0; j < _count - 1 - i; j++)
                {
                    if (Compare(_items[j], _items[j + 1], field, ascending) > 0)
                    {
                        Product tmp = _items[j];
                        _items[j] = _items[j + 1];
                        _items[j + 1] = tmp; // FIX: swap đúng chỉ số
                    }
                }
            }
        }

        private int Compare(Product a, Product b, SortField field, bool asc)
        {
            int c = 0;
            switch (field)
            {
                case SortField.Code:      c = string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase); break;
                case SortField.Name:      c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); break;
                case SortField.Quantity:  c = a.Quantity.CompareTo(b.Quantity); break;
                case SortField.SellPrice: c = a.SellPrice.CompareTo(b.SellPrice); break;
            }
            return asc ? c : -c;
        }

        public int TotalQuantity()
        {
            int sum = 0; for (int i = 0; i < _count; i++) sum += _items[i].Quantity; return sum;
        }
        public decimal TotalInventoryValue()
        {
            decimal sum = 0; for (int i = 0; i < _count; i++) sum += _items[i].InventoryValue(); return sum;
        }
        public decimal TotalProfitEstimate()
        {
            decimal sum = 0; for (int i = 0; i < _count; i++) sum += _items[i].ProfitEstimate(); return sum;
        }

        public void ReportOutOrLow()
        {
            int threshold = RestockThreshold;
            Console.WriteLine("Sản phẩm hết/sắp hết (≤ " + threshold + "):");
            bool any = false;
            for (int i = 0; i < _count; i++)
            {
                Product p = _items[i];
                if (p.Quantity <= threshold)
                {
                    Console.WriteLine("- " + p.Code + " | " + p.Name + " | SL: " + p.Quantity);
                    any = true;
                }
            }
            if (!any) Console.WriteLine("(Không có)");
        }

        public void Print2DMatrixByCategoryMonth()
        {
            Console.WriteLine("Bảng (Loại × Tháng) = SL nhập/thêm (ghi nhận khi Thêm):");
            Console.Write("Loại\\Tháng");
            for (int m = 1; m <= 12; m++) Console.Write("\t" + m);
            Console.WriteLine();

            int catN = Enum.GetValues(typeof(Category)).Length;
            for (int c = 0; c < catN; c++)
            {
                Console.Write(((Category)c).ToString());
                for (int m = 0; m < 12; m++) Console.Write("\t" + _byCategoryByMonth[c, m]);
                Console.WriteLine();
            }
        }

        public bool SaveToFile(string path = null)
        {
            if (string.IsNullOrEmpty(path)) path = DefaultDataFile;
            try
            {
                if (File.Exists(path))
                {
                    string dir = Path.GetDirectoryName(path); if (dir == null) dir = "";
                    string bak = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak");
                    File.Copy(path, bak, true);
                }
                using (var sw = new StreamWriter(path, false, new UTF8Encoding(true)))
                {
                    for (int i = 0; i < _count; i++) sw.WriteLine(_items[i].ToCsv());
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Lỗi ghi file]: " + ex.Message);
                return false;
            }
        }

        public bool LoadFromFile(string path = null)
        {
            if (string.IsNullOrEmpty(path)) path = DefaultDataFile;
            if (!File.Exists(path)) { Console.WriteLine("File dữ liệu chưa tồn tại, bỏ qua đọc."); return false; }

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                _items = new Product[Math.Max(InitialCapacity, lines.Length)];
                _count = 0;
                _byCategoryByMonth = new int[Enum.GetValues(typeof(Category)).Length, 12];

                for (int i = 0; i < lines.Length; i++)
                {
                    Product p;
                    if (Product.TryParseCsv(lines[i], out p))
                    {
                        EnsureCapacity();
                        _items[_count++] = p;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Lỗi đọc file]: " + ex.Message);
                return false;
            }
        }
    }

    class Program
    {
        static CultureInfo vi = new CultureInfo("vi-VN");

        static readonly string[] Menu = new string[]
        {
            "1. Thêm sản phẩm",
            "2. Sửa sản phẩm",
            "3. Xoá sản phẩm",
            "4. Hiển thị danh sách",
            "5. Tìm kiếm (Linear: mã/tên)",
            "6. Sắp xếp (Bubble: mã/tên/SL/giá)",
            "7. Thống kê nhanh (tổng SL, tổng giá trị, LN dự kiến)",
            "8. Báo cáo: Hết/sắp hết (mặc định ≤ 5)",
            "9. Bảng 2D (Loại × Tháng)",
            "10. Lưu dữ liệu ra file",
            "11. Đọc dữ liệu từ file",
            "12. Hướng dẫn sử dụng",
            "0. Thoát (xác nhận & tự động lưu)"
        };

        static void PrintMenu()
        {
            Console.WriteLine("\n===== MENU QUẢN LÝ KHO =====");
            for (int i = 0; i < Menu.Length; i++) Console.WriteLine(Menu[i]);
            Console.Write("Chọn: ");
        }

        static void PrintHelp()
        {
            Console.WriteLine("=== HƯỚNG DẪN SỬ DỤNG ===");
            Console.WriteLine("- 1 Thêm: Nhập mã (duy nhất), tên, loại (0..4), số lượng>=0, giá nhập/bán>=0. Có thể nhập tháng (1..12; 0 = tháng hiện tại).");
            Console.WriteLine("- 2 Sửa: Nhập mã cần sửa; nhập giá trị mới (bỏ trống = giữ nguyên). Cho phép đổi loại, số lượng, giá.");
            Console.WriteLine("- 3 Xoá: Nhập mã và xác nhận xoá (y/n).");
            Console.WriteLine("- 4 Hiển thị: In toàn bộ danh sách kèm giá trị tồn & lợi nhuận dự kiến.");
            Console.WriteLine("- 5 Tìm kiếm: Tìm theo chuỗi con trong MÃ hoặc TÊN (không phân biệt hoa/thường).");
            Console.WriteLine("- 6 Sắp xếp: Chọn trường 0=Mã,1=Tên,2=SL,3=Giá bán; a=asc, d=desc.");
            Console.WriteLine("- 7 Thống kê nhanh: Tổng SL, Tổng giá trị hàng (giá bán), Tổng LN dự kiến.");
            Console.WriteLine("- 8 Hết/sắp hết: Liệt kê sản phẩm có SL ≤ 5 (ngưỡng cố định).");
            Console.WriteLine("- 9 Bảng 2D: Ma trận (Loại × Tháng) SL đã thêm (ghi nhận lúc Thêm).");
            Console.WriteLine("- 10 Lưu: Lưu ra file CSV (tạo file .bak nếu ghi đè).");
            Console.WriteLine("- 11 Đọc: Nạp dữ liệu từ CSV vào chương trình.");
            Console.WriteLine("- 12 Help: Hiển thị hướng dẫn này.");
            Console.WriteLine("- 0 Thoát: Xác nhận và TỰ LƯU về " + ProductRepository.DefaultDataFile + ".");
            Console.WriteLine("* Mẹo: Trên Windows, gõ 'chcp 65001' trước khi chạy để hiển thị tiếng Việt đẹp.");
        }

        static string ReadNonEmpty(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                Console.WriteLine("Không được rỗng, vui lòng nhập lại.");
            }
        }
        static int ReadInt(string prompt, int min)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine();
                int v; if (int.TryParse(s, out v) && v >= min) return v;
                Console.WriteLine("Giá trị không hợp lệ. Yêu cầu >= " + min + ".");
            }
        }
        static decimal ReadDecimal(string prompt, decimal min)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine(); if (s == null) s = ""; s = s.Trim();
                decimal v;
                if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v) && v >= min) return v;
                if (decimal.TryParse(s, NumberStyles.Any, vi, out v) && v >= min) return v;
                Console.WriteLine("Giá trị không hợp lệ. Yêu cầu >= " + min + ".");
            }
        }
        static Category ReadCategory(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine();
                int c; if (int.TryParse(s, out c) && c >= 0 && c <= 4) return (Category)c;
                Console.WriteLine("Loại không hợp lệ.");
            }
        }
        static Category ReadCategory() { return ReadCategory("Chọn loại (0:Thực phẩm,1:Điện tử,2:Quần áo,3:Gia dụng,4:Khác): "); }

        static void PrintRestockSuggestion(ProductRepository repo, int threshold = ProductRepository.RestockThreshold)
        {
            Console.WriteLine("Gợi ý nhập hàng (SL ≤ " + threshold + "):");
            repo.ReportOutOrLow();
        }

        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            try
            {
                ProductRepository repo = new ProductRepository();
                repo.LoadFromFile(ProductRepository.DefaultDataFile);

                bool running = true;
                while (running)
                {
                    PrintMenu();
                    string choice = Console.ReadLine(); if (choice == null) choice = ""; choice = choice.Trim();

                    switch (choice)
                    {
                        case "1":
                        {
                            string code = ReadNonEmpty("Mã SP: ");
                            if (repo.ExistsCode(code)) { Console.WriteLine("Mã đã tồn tại!"); break; }
                            string name = ReadNonEmpty("Tên SP: ");
                            Category cat = ReadCategory();
                            int qty = ReadInt("Số lượng (>=0): ", 0);
                            decimal cost = ReadDecimal("Giá nhập (>=0): ", 0);
                            decimal sell = ReadDecimal("Giá bán (>=0): ", 0);
                            int month = ReadInt("Tháng nhập (1-12, 0 = tháng hiện tại): ", 0);
                            if (repo.Add(code, name, cat, qty, cost, sell, month))
                                Console.WriteLine("Đã thêm.");
                            else
                                Console.WriteLine("Thêm thất bại (kiểm tra dữ liệu).");
                            break;
                        }
                        case "2":
                        {
                            string code = ReadNonEmpty("Nhập mã SP cần sửa: ");

                            Console.Write("Tên mới (bỏ trống = giữ nguyên): ");
                            string newName = Console.ReadLine();

                            Console.Write("Loại mới (0..4, bỏ trống = giữ nguyên): ");
                            string sType = Console.ReadLine();
                            Category? newType = null; int tVal;
                            if (int.TryParse(sType, out tVal) && tVal >= 0 && tVal <= 4) newType = (Category)tVal;

                            Console.Write("SL mới (>=0, bỏ trống = giữ nguyên): ");
                            string sQty = Console.ReadLine(); int qVal; int? newQty = null;
                            if (int.TryParse(sQty, out qVal) && qVal >= 0) newQty = qVal;

                            Console.Write("Giá nhập mới (>=0, bỏ trống = giữ nguyên): ");
                            string sCost = Console.ReadLine(); decimal c1; decimal? newCost = null;
                            if (decimal.TryParse(sCost, NumberStyles.Any, CultureInfo.InvariantCulture, out c1) && c1 >= 0) newCost = c1;
                            else if (decimal.TryParse(sCost, NumberStyles.Any, vi, out c1) && c1 >= 0) newCost = c1;

                            Console.Write("Giá bán mới (>=0, bỏ trống = giữ nguyên): ");
                            string sSell = Console.ReadLine(); decimal c2; decimal? newSell = null;
                            if (decimal.TryParse(sSell, NumberStyles.Any, CultureInfo.InvariantCulture, out c2) && c2 >= 0) newSell = c2;
                            else if (decimal.TryParse(sSell, NumberStyles.Any, vi, out c2) && c2 >= 0) newSell = c2;

                            if (repo.Update(code, newName, newType, newQty, newCost, newSell))
                                Console.WriteLine("Đã cập nhật.");
                            else
                                Console.WriteLine("Không tìm thấy mã hoặc dữ liệu không hợp lệ.");
                            break;
                        }
                        case "3":
                        {
                            string code = ReadNonEmpty("Nhập mã SP cần xoá: ");
                            Console.Write("Xác nhận xoá (y/n): ");
                            string yn = Console.ReadLine(); yn = (yn == null) ? "" : yn.Trim().ToLower();
                            if (yn == "y" || yn == "yes")
                            {
                                if (repo.Remove(code)) Console.WriteLine("Đã xoá."); else Console.WriteLine("Không tìm thấy.");
                            }
                            else Console.WriteLine("Đã huỷ xoá.");
                            break;
                        }
                        case "4":
                        {
                            repo.PrintAll(vi);
                            break;
                        }
                        case "5":
                        {
                            Console.Write("Nhập từ khoá (mã hoặc tên): ");
                            string kw = Console.ReadLine();
                            repo.LinearSearch(kw);
                            break;
                        }
                        case "6":
                        {
                            Console.WriteLine("Chọn trường: 0=Mã, 1=Tên, 2=Số lượng, 3=Giá bán");
                            int f = ReadInt("Trường: ", 0);
                            if (f < 0 || f > 3) f = 0;
                            Console.Write("Thứ tự (a=asc, d=desc): ");
                            string ord = Console.ReadLine(); bool asc = true;
                            if (ord != null && ord.Trim().ToLower() == "d") asc = false;
                            repo.BubbleSort((ProductRepository.SortField)f, asc);
                            Console.WriteLine("Đã sắp xếp.");
                            break;
                        }
                        case "7":
                        {
                            Console.WriteLine("Tổng SL tồn: " + repo.TotalQuantity());
                            Console.WriteLine("Tổng giá trị tồn (giá bán): " + repo.TotalInventoryValue().ToString("N2", vi));
                            Console.WriteLine("Tổng lợi nhuận dự kiến: " + repo.TotalProfitEstimate().ToString("N2", vi));
                            break;
                        }
                        case "8":
                        {
                            Console.WriteLine("(Ngưỡng sắp hết mặc định: ≤ " + ProductRepository.RestockThreshold + ")");
                            PrintRestockSuggestion(repo);
                            break;
                        }
                        case "9":
                        {
                            repo.Print2DMatrixByCategoryMonth();
                            break;
                        }
                        case "10":
                        {
                            Console.Write("Đường dẫn file (Enter = " + ProductRepository.DefaultDataFile + "): ");
                            string path = Console.ReadLine();
                            if (string.IsNullOrWhiteSpace(path)) path = ProductRepository.DefaultDataFile;
                            if (repo.SaveToFile(path)) Console.WriteLine("Đã lưu (có tạo .bak nếu ghi đè).");
                            break;
                        }
                        case "11":
                        {
                            Console.Write("Đường dẫn file (Enter = " + ProductRepository.DefaultDataFile + "): ");
                            string path = Console.ReadLine();
                            if (string.IsNullOrWhiteSpace(path)) path = ProductRepository.DefaultDataFile;
                            repo.LoadFromFile(path);
                            break;
                        }
                        case "12":
                        {
                            PrintHelp();
                            break;
                        }
                        case "0":
                        {
                            Console.Write("Xác nhận thoát? (sẽ TỰ LƯU vào " + ProductRepository.DefaultDataFile + ") (y/n): ");
                            string yn = Console.ReadLine(); yn = (yn == null) ? "" : yn.Trim().ToLower();
                            if (yn == "y" || yn == "yes")
                            {
                                repo.SaveToFile(ProductRepository.DefaultDataFile);
                                running = false;
                            }
                            break;
                        }
                        default:
                            Console.WriteLine("Lựa chọn không hợp lệ.");
                            break;
                    }
                }

                Console.WriteLine("Tạm biệt!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Lỗi tổng quát] Chương trình đã được bảo vệ khỏi crash.");
                Console.WriteLine("Chi tiết: " + ex.Message);
            }
        }
    }
}


