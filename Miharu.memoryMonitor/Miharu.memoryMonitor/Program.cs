using System;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;


namespace Miharu.MemoryMonitor
{
    public class Program
    {
         public static string GetLoadAverage(Process exe){

            //ComSpec(cmd.exe)のパスを取得して、FileNameプロパティに指定
            exe.StartInfo.FileName = System.Environment.GetEnvironmentVariable("ComSpec");

            // 出力を読み取れるようにする
            exe.StartInfo.UseShellExecute = false;
            exe.StartInfo.RedirectStandardOutput = true;
            exe.StartInfo.RedirectStandardInput = false;

            exe.StartInfo.CreateNoWindow = true;
            string typeperfPath = @"C:\Windows\System32\typeperf" + " ";
            string command = @"""System\Processor Queue Length""" + " " + "-sc 1" + " -si " + "0";
            exe.StartInfo.Arguments = "/c " + typeperfPath + " " + command;
            //起動
            exe.Start();
            //出力を読み取る
            string results = "result = " + exe.StandardOutput.ReadToEnd() + "   " + Environment.NewLine;

            //ロードアベレージの文字列だけを取り出す方法をいろいろ試し、インデックス101で出たので採用した
            string LoadAverage = results.Substring(101);

            //ロードアベレージの数値の部分だけを取り出すため、それ以外の文字列を削除している
            LoadAverage = LoadAverage.Remove(0, 1);
            LoadAverage = LoadAverage.Remove(8);

            return LoadAverage;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("対象プロセス名を入力");
            string str = Console.ReadLine();
            Console.WriteLine("取得周期を入力[秒](誤差＋1秒あり)");
            int period = 1;
            try
            {
                period = int.Parse(Console.ReadLine()) * 1000;
            }
            catch (FormatException ex)
            {
                Console.WriteLine("エラー: {0} ", ex.Message);
                System.Threading.Thread.Sleep(1000);
                return;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("エラー: {0} ", ex.Message);
                System.Threading.Thread.Sleep(1000);
                return;
            }

            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\memoryMonitor.csv";
            string header = "日時," + "プロセス名," + "物理メモリ使用量[KB]," + "仮想メモリ使用量[KB],"
                + "ページドメモリサイズ[KB]," + "ロードアベレージ";

            Console.WriteLine(header);
            //ロードアベレージはprocessor queue lengthを採用

            Encoding utf8 = Encoding.GetEncoding("utf-8");

            using (var headWriter = new StreamWriter(path, true, utf8))
            {
                headWriter.Write(header + Environment.NewLine);
            }

            Process[] ps = Process.GetProcesses();
            var p = ps.FirstOrDefault(a => a.ProcessName == str);

            if (p == null)
            {
                Console.WriteLine("fault");
                System.Threading.Thread.Sleep(1000);
                return;
            }

            Item item = new Item();
            Process exe = new Process();
            using (StreamWriter writer = new StreamWriter(path, true, utf8))
            {
                while (true)
                {
                    try
                    {
                        string LoadAverage = GetLoadAverage(exe);

                        //ワーキングセットを物理メモリ使用量とした
                        item.PhyMemory = p.WorkingSet64 / 1024;
                        item.ImaMemory = p.VirtualMemorySize64 / 1024;
                        item.PagingMemory = p.PagedMemorySize64 / 1024;

                        string log = string.Format("{0}," + "{1}," + "{2}," + "{3}," + "{4}," + LoadAverage + Environment.NewLine,
                                    DateTime.Now.ToString("yyyy/MM/dd/HH:mm:ss"), p.ProcessName, item.PhyMemory, item.ImaMemory, item.PagingMemory);
                        Console.Write(log);
                        writer.Write(log);
                        writer.Flush();
                        p.Refresh();

                        //プロセス終了まで待機する
                        //WaitForExitはReadToEndの後である必要がある
                        //(親プロセス、子プロセスでブロック防止のため)
                        exe.WaitForExit();
                        exe.Close();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("エラー: {0}", ex.Message);
                    }
                    System.Threading.Thread.Sleep(period);
                }
            }

        }
    }
}


class Item
{
    public long PhyMemory { get; set; }
    public long ImaMemory { get; set; }
    public long PagingMemory { get; set; }
}