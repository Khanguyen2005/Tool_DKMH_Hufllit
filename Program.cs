using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using HtmlAgilityPack;
using System.Linq;
using System.Net;

namespace ToolDKMH
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== TOOL TỰ ĐỘNG ĐĂNG KÝ HỌC PHẦN HUFLIT ===");

            Console.Write("\n🔐 Dán cookie HUFLIT của bạn vào đây: ");
            string cookie = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(cookie))
            {
                Console.WriteLine("❌ Cookie không được để trống.");
                return;
            }

            string filePath = "C:\\Users\\Admin\\Desktop\\MyStudy\\ConsoleApp\\ToolDKMH\\TKB.txt";

            if (!File.Exists(filePath))
            {
                Console.WriteLine("❌ Không tìm thấy file TKB.txt");
                return;
            }

            List<(string MaHP, string[] MaLHPs)> danhSachMon = new();
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(':');
                if (parts.Length != 2) continue;
                string maHP = parts[0].Trim();
                string[] maLHPs = parts[1].Split('|', StringSplitOptions.RemoveEmptyEntries);
                danhSachMon.Add((maHP, maLHPs.Select(x => x.Trim()).ToArray()));
            }

            HttpClient client = new();
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            while (true)
            {
                Console.WriteLine("\n📋 Chọn chức năng:");
                Console.WriteLine("1. Tự động đăng ký các lớp học phần trong TKB.txt");
                Console.WriteLine("2. Thoát");
                Console.Write("👉 Nhập lựa chọn của bạn: ");

                var choice = Console.ReadLine();

                if (choice == "1")
                {
                    foreach (var mon in danhSachMon)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"\n⏳ Đang xử lý môn {mon.MaHP}...");
                        Console.ResetColor();

                        string encodedId = await AutoDKHelper.GetEncodedIdFromHP(client, mon.MaHP);
                        if (string.IsNullOrEmpty(encodedId))
                        {
                            Console.WriteLine("❌ Không tìm được mã encode của môn.");
                            continue;
                        }

                        string url = $"https://dkmh.huflit.edu.vn/DangKyHocPhan/DanhSachLopHocPhan?id={encodedId}&registType=KH";
                        var html = await client.GetStringAsync(url);
                        await Task.Delay(200); // ✅ tránh lỗi timing khi tải HTML

                        string lhpEncoded = AutoDKHelper.FindFirstAvailableLHP(html, mon.MaLHPs);
                        if (lhpEncoded == null)
                        {
                            Console.WriteLine("⚠️ Không tìm thấy lớp học phần phù hợp hoặc còn chỗ trống.");
                            continue;
                        }

                        string dkUrl = $"https://dkmh.huflit.edu.vn/DangKyHocPhan/RegistUpdateScheduleStudyUnit?Hide={lhpEncoded}|&ScheduleStudyUnitOld=&acceptConflict=";
                        var res = await client.GetAsync(dkUrl);

                        if (res.IsSuccessStatusCode)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✅ Đăng ký thành công!");

                            // Kiểm tra kết quả trong danh sách đã đăng ký
                            var kq = await client.GetStringAsync("https://dkmh.huflit.edu.vn/DangKyHocPhan/KetQuaDangKy/1");
                            if (mon.MaLHPs.Any(m => kq.Contains(m)))
                            {
                                Console.WriteLine($"✅ Xác nhận: Lớp {mon.MaLHPs.First(m => kq.Contains(m))} đã được đăng ký.");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("⚠️ Không tìm thấy trong danh sách kết quả.");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("❌ Đăng ký thất bại!");
                            Console.ResetColor();
                        }
                    }
                }
                else if (choice == "2")
                {
                    Console.WriteLine("👋 Tạm biệt!");
                    break;
                }
                else
                {
                    Console.WriteLine("⚠️ Lựa chọn không hợp lệ!");
                }
            }
        }
    }

    static class AutoDKHelper
    {
        private static readonly Dictionary<string, string> MaHPToEncodedId = new()
        {
            { "1010472", "+8zRAlMtdfOZBRNZgYXmCQ==" }
        };

        public static async Task<string> GetEncodedIdFromHP(HttpClient client, string maHP)
        {
            if (MaHPToEncodedId.TryGetValue(maHP, out var encoded))
                return encoded;

            return null;
        }

        public static string FindFirstAvailableLHP(string html, string[] maLHPs)
        {
            HtmlDocument doc = new();
            doc.LoadHtml(html);

            var inputs = doc.DocumentNode.SelectNodes("//input[@type='radio']");
            if (inputs == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Không tìm thấy radio button lớp học phần.");
                Console.ResetColor();
                return null;
            }

            foreach (var input in inputs)
            {
                var id = input.GetAttributeValue("id", "");
                var tr = input.ParentNode?.ParentNode;
                var columns = tr?.SelectNodes("td");
                if (columns == null || columns.Count < 5)
                {
                    Console.WriteLine("⚠️ Dòng dữ liệu lớp học phần không hợp lệ.");
                    continue;
                }

                string maLHP = WebUtility.HtmlDecode(columns[2].InnerText.Trim());
                string slCon = WebUtility.HtmlDecode(columns[4].InnerText.Trim());


                if (maLHPs.Contains(maLHP) && int.TryParse(slCon, out int soLuong) && soLuong > 0)
                    return WebUtility.UrlEncode(id);
            }

            return null;
        }
    }
}
