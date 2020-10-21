using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace FileUpdateChecker
{
    public partial class Form1 : Form
    {
        string _XmlPath = Application.StartupPath + @"\dat_path.xml";
        string _XmlFileList = Application.StartupPath + @"\dat_file_list.xml";

        Timer _MainTimer = new Timer();
        NotifyIcon _NotifyIcon = new NotifyIcon();

        List<MyFileInfo> _BeforeFileList = new List<MyFileInfo>();

        public Form1()
        {
            InitializeComponent();

            EventAttach();
            SetNotifyIcon();

            _MainTimer.Interval = 1000 * 60; // 1分
            _MainTimer.Tick += (s, e) => TimerWork();

            ButtonStateChange(ButtonState.waiting);

            if (File.Exists(_XmlPath))
            {
                string path = "";
                using (StreamReader sr = new StreamReader(_XmlPath, Encoding.UTF8))
                {
                    path = (string)new XmlSerializer(typeof(string)).Deserialize(sr);
                }

                if (Directory.Exists(path))
                {
                    TargetFolderPath.Text = path;
                    // PathのXmlが存在し、Pathが有効ならTextboxへ

                    if (File.Exists(_XmlFileList))
                    {
                        using (StreamReader sr = new StreamReader(_XmlFileList, Encoding.UTF8))
                        {
                            _BeforeFileList = new List<MyFileInfo>((List<MyFileInfo>)new XmlSerializer(typeof(List<MyFileInfo>)).Deserialize(sr));
                        }

                        // 自動起動するのはXMLが2つ有った場合
                        // FileListのXmlはストップ時削除するので、2つある場合、ストップしていないということ
                        _MainTimer.Start();

                        ButtonStateChange(ButtonState.working);
                        WindowState = FormWindowState.Minimized;
                        ShowInTaskbar = false;
                    }
                }
            }
        }

        private void EventAttach()
        {
            this.ClientSizeChanged += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    ShowInTaskbar = false;
                }
            };

            this.FormClosing += (s, e) =>
            {
                DialogResult yesNo = MessageBox.Show("終了しますか？", "", MessageBoxButtons.YesNo);
                if (yesNo == DialogResult.No) e.Cancel = true;   
            };

            this.FormClosed += (s, e) =>
            {
                _NotifyIcon.Dispose();
            };

            StartButton.Click += (s, e) => StartWatch();
            StopButton.Click += (s, e) => StopWatch();
        }

        private Icon[] CreateIconImageArray()
        {
            Bitmap yellowGreenBitmap = new Bitmap(32, 32);
            Graphics yellowGreenGraphics = Graphics.FromImage(yellowGreenBitmap);
            yellowGreenGraphics.FillRectangle(Brushes.YellowGreen, yellowGreenGraphics.VisibleClipBounds);

            Bitmap greenBitmap = new Bitmap(32, 32);
            Graphics greenGraphics = Graphics.FromImage(greenBitmap);
            greenGraphics.FillRectangle(Brushes.Green, greenGraphics.VisibleClipBounds);

            return new Icon[] {
                Icon.FromHandle(yellowGreenBitmap.GetHicon()),
                Icon.FromHandle(greenBitmap.GetHicon())
            };
        }

        private void SetNotifyIcon()
        {
            Icon[] icons = CreateIconImageArray();
            _NotifyIcon.Visible = true;
            _NotifyIcon.Click += new EventHandler((object sender, EventArgs e) => {
                WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
            });

            int i = 0;
            var t = new Timer();
            t.Interval = 700;
            t.Tick += (s, e) =>
            {
                _NotifyIcon.Icon = icons[i++];
                if (i == (icons.Count())) i = 0;
            };
            t.Start();
        }

        private void TimerWork()
        {
            List<string> message = new List<string>();

            if (_BeforeFileList.Count > 0)
            {
                foreach(var currentFileInfo in GetCurrentMyFileInfoList())
                {
                    bool isNew = true;
                    bool isUpdate = false;

                    foreach (var beforeFileInfo in _BeforeFileList)
                    {
                        if (currentFileInfo.Name == beforeFileInfo.Name)
                        {
                            isNew = false;
                            if (currentFileInfo.Length != beforeFileInfo.Length) isUpdate = true;
                        }
                    }

                    if (isNew) message.Add("新規：　" + currentFileInfo.Name);
                    if (isUpdate) message.Add("更新：　" + currentFileInfo.Name);
                }
            }

            _BeforeFileList.Clear();
            _BeforeFileList = new List<MyFileInfo>(GetCurrentMyFileInfoList());

            using (StreamWriter sw = new StreamWriter(_XmlFileList, false, Encoding.UTF8))
            {
                new XmlSerializer(typeof(List<MyFileInfo>)).Serialize(sw, _BeforeFileList);
            }

            if (message.Count > 0) MessageBox.Show(string.Join(Environment.NewLine, message));
        }

        private List<MyFileInfo> GetCurrentMyFileInfoList()
        {
            List<MyFileInfo> fileInfoList = new List<MyFileInfo>();

            try
            {
                foreach (string file in Directory.GetFiles(TargetFolderPath.Text, "*", SearchOption.TopDirectoryOnly))
                {
                    var fi = new FileInfo(file);
                    fi.Refresh();
                    fileInfoList.Add(new MyFileInfo
                    {
                        Name = fi.Name,
                        Length = fi.Length
                    });
                }
            }
            catch
            {
                fileInfoList.Clear();
            }
            return fileInfoList;
        }

        private void StartWatch()
        {
            if(!Directory.Exists(TargetFolderPath.Text))
            {
                MessageBox.Show("フォルダが存在しません。");
                return;
            }

            using (StreamWriter sw = new StreamWriter(_XmlPath, false, Encoding.UTF8))
            {
                new XmlSerializer(typeof(string)).Serialize(sw, TargetFolderPath.Text);
            }

            TimerWork(); 
            // TimerのTickが実行される前に終了した場合、_XmlFileListが生成されていないため
            // これで2つのXmlが存在する状態になる

            _MainTimer.Start();

            ButtonStateChange(ButtonState.working);
        }

        private void StopWatch()
        {
            _BeforeFileList.Clear();
            File.Delete(_XmlFileList);
            // 終了時、Xmlは1つ(Pathのみ)となる

            _MainTimer.Stop();

            ButtonStateChange(ButtonState.waiting);
        }

        enum ButtonState { working, waiting }

        private void ButtonStateChange(ButtonState buttonState)
        {
            switch (buttonState)
            {
                case ButtonState.waiting:
                    StartButton.Enabled = true;
                    StopButton.Enabled = false;
                    TargetFolderPath.Enabled = true;
                    this.Text = "停止中";
                    break;

                case ButtonState.working:
                    StartButton.Enabled = false;
                    StopButton.Enabled = true;
                    TargetFolderPath.Enabled = false;
                    this.Text = "監視中";
                    break;
            }
        }
    }

    public class MyFileInfo
    {
        public string Name;
        public long Length;
    }

}
