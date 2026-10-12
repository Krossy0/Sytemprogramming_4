using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

class Program
{
    class DownloadItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Size { get; set; }
        public double Downloaded { get; set; }
        public int Status { get; set; } = 0;
    }

    static List<DownloadItem> downloads = new List<DownloadItem>();

    static Semaphore semaphore = new Semaphore(3, 3);
    static CancellationTokenSource cts = new CancellationTokenSource();

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        downloads.Add(new DownloadItem
        {
            Id = 1,
            Name = "mp4_sample_file_25MB.mp4",
            Size = 25
        });

        downloads.Add(new DownloadItem
        {
            Id = 2,
            Name = "mp4_sample_file_50MB.mp4",
            Size = 50
        });

        downloads.Add(new DownloadItem
        {
            Id = 3,
            Name = "mp4_sample_file_100MB.mp4",
            Size = 99.2
        });

        downloads.Add(new DownloadItem
        {
            Id = 4,
            Name = "mp4_sample_file_200MB.mp4",
            Size = 200
        });

        Console.Clear();

        Thread keyboardThread = new Thread(CheckKeyboard);
        keyboardThread.Start();

        List<Thread> workers = new List<Thread>();

        for (int i = 0; i < downloads.Count; i++)
        {
            int index = i;

            Thread worker = new Thread(() => DownloadFile(index));

            workers.Add(worker);
            worker.Start();
        }

        bool finished = false;

        while (!cts.Token.IsCancellationRequested && !finished)
        {
            DrawUI();

            lock (downloads)
            {
                finished = true;

                foreach (DownloadItem file in downloads)
                {
                    if (file.Status != 2 && file.Status != 3)
                    {
                        finished = false;
                        break;
                    }
                }
            }

            Thread.Sleep(100);
        }

        foreach (Thread worker in workers)
        {
            worker.Join();
        }

        cts.Cancel();
        keyboardThread.Join();

        DrawUI();

        Console.WriteLine("\nBütün yükləmələr dayandı və ya tamamlandı.");
        Console.WriteLine("Çıxmaq üçün Enter basın.");
        Console.ReadLine();

        semaphore.Dispose();
        cts.Dispose();
    }

    static void DownloadFile(int index)
    {
        bool entered = false;

        try
        {
            semaphore.WaitOne();
            entered = true;

            lock (downloads)
            {
                downloads[index].Status = 1;
            }

            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();

                lock (downloads)
                {
                    if (downloads[index].Downloaded >= downloads[index].Size)
                    {
                        downloads[index].Status = 2;
                        break;
                    }

                    downloads[index].Downloaded += 5;

                    if (downloads[index].Downloaded > downloads[index].Size)
                    {
                        downloads[index].Downloaded = downloads[index].Size;
                    }
                }

                Thread.Sleep(200);
            }
        }
        catch (OperationCanceledException)
        {
            lock (downloads)
            {
                if (downloads[index].Status != 2)
                {
                    downloads[index].Status = 3;
                }
            }
        }
        finally
        {
            if (entered)
            {
                semaphore.Release();
            }
        }
    }

    static void CheckKeyboard()
    {
        while (!cts.Token.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                ConsoleKey key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.Escape)
                {
                    cts.Cancel();
                    break;
                }
            }

            Thread.Sleep(100);
        }
    }

    static void DrawUI()
    {
        lock (downloads)
        {
            Console.SetCursorPosition(0, 0);

            Console.WriteLine("==================================================");
            Console.WriteLine("              VIDEO DOWNLOAD MANAGER             ");
            Console.WriteLine("              Maximum downloads: 3               ");
            Console.WriteLine("==================================================");
            Console.WriteLine("ESC - Cancel all downloads\n");

            int active = 0;
            int waiting = 0;
            int completed = 0;
            int cancelled = 0;

            foreach (DownloadItem file in downloads)
            {
                double percent = file.Downloaded / file.Size * 100;

                Console.Write($"File #{file.Id}: ");

                if (file.Status == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Waiting");
                    waiting++;
                }
                else if (file.Status == 1)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("Downloading");
                    active++;
                }
                else if (file.Status == 2)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Completed");
                    completed++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Cancelled");
                    cancelled++;
                }

                Console.ResetColor();

                Console.WriteLine($" - {percent:0}%");
                Console.WriteLine($"  {file.Name}");

                DrawProgressBar(percent);

                Console.WriteLine(
                    $"  {file.Downloaded:F1} MB / {file.Size:F1} MB\n");
            }

            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine(
                $"Active: {active} | Waiting: {waiting} | " +
                $"Completed: {completed} | Cancelled: {cancelled}");

            Console.WriteLine("--------------------------------------------------");
        }
    }

    static void DrawProgressBar(double percent)
    {
        int width = 25;
        int filled = (int)(percent / 100 * width);

        Console.Write("[");

        for (int i = 0; i < width; i++)
        {
            if (i < filled)
                Console.Write("#");
            else
                Console.Write("-");
        }

        Console.WriteLine("]");
    }
}
