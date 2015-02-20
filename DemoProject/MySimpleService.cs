using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceHosting;
using System.Timers;

namespace DemoProject
{
    public class MySimpleService : IHostedService
    {
        private readonly Timer _timer;

        public MySimpleService()
        {
            _timer = new Timer(TimeSpan.FromSeconds(5).TotalMilliseconds);
            _timer.Elapsed += new ElapsedEventHandler(TimerElaspedHandler);
            _timer.Enabled = false;
        }

        public void Start(string[] args)
        {
            _timer.Start();
        }

        public void Terminate()
        {
            // Check that the timer is stopped
            try
            {
                if (_timer.Enabled)
                {
                    _timer.Stop();
                }
            }
            finally
            {
                _timer.Dispose();
            }
        }

        public void BeginShutdown()
        {
            _timer.Stop();
        }

        static void TimerElaspedHandler(object sender, ElapsedEventArgs e)
        {
            if (Environment.UserInteractive)
            {
                Console.WriteLine("Current Time is: {0}", DateTime.Now.ToLongTimeString());
            }
            else
            {
                using (var file = System.IO.File.Open("Logfile.txt", System.IO.FileMode.OpenOrCreate))
                {
                    using (var writer = new System.IO.StreamWriter(file))
                    {
                        writer.WriteLine("Current Time is: {0}", DateTime.Now.ToLongTimeString());
                    }
                }
            }
        }

    }
}
