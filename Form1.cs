using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web;

namespace GetCourseInfoV2
{

    public sealed partial class Form1 : Form
    {
        const string _varStr = "V1.0 Build 10";
        private const string _MainPageUrl = "http://learn.tsinghua.edu.cn/MultiLanguage/lesson/student/MyCourse.jsp?language=cn";

        public Form1()
        {
            InitializeComponent();
            webBrowser1.ScriptErrorsSuppressed = true;
            Text = "本科生网络学堂更新检测器 " + _varStr;
        }

        class NoLoginIn : Exception
        {
            public NoLoginIn()
            {
            }
        }

        TsinghuaCourseInfo GetTsinghuaCourseInfoHelper()
        {
            var helper = TsinghuaCourseInfo.LoadFromDataFile();

            if (helper == null)
            {
                Invoke(new Action(() =>
                {
                    using (var loginForm = new LoginForm())
                    {
                        var dialogResult = loginForm.ShowDialog();
                        if (dialogResult != DialogResult.Yes)
                        {
                            menuStrip1.Enabled = true;
                            显示网络学堂主界面ToolStripMenuItem.Enabled = false;
                            throw new NoLoginIn();
                        }

                        var userId = loginForm.textBox1.Text;
                        var password = loginForm.maskedTextBox1.Text;
                        helper = new TsinghuaCourseInfo(userId, password);
                    }
                }));
            }

            return helper;
        }

        static IEnumerable<ViewListItemData> GetItemDataList(TsinghuaCourseInfo helper)
        {
            var newItems = helper.GetNewItems();
            var newfiles = newItems.Select(e => e as CourseFileInfo)
                                    .Where(e => e != null)
                                    .ToArray();

            var itemOnShow = new List<ViewListItemData>();

            var normalItems = newItems
                .Where(e => !(e is CourseFileInfo))
                .Select(e => new ViewListItemData_Normal(e))
                .Cast<ViewListItemData>();
            itemOnShow.AddRange(normalItems);

            var fileListItems = newfiles
                .GroupBy(e => e.Course)
                .Select(g => g.ToArray())
                .Select(e => new ViewListItemData_FileList(e))
                .Cast<ViewListItemData>();
            itemOnShow.AddRange(fileListItems);
           
            itemOnShow.Sort();

            return itemOnShow;
        }

        void UpdateInfo_MainThread(IEnumerable<ViewListItemData> itemOnShow)
        {
            显示网络学堂主界面ToolStripMenuItem.Enabled = true;

            var viewItemList = new List<ListViewItem>();

            foreach (var item in itemOnShow)
            {
                var listViewItem = new ListViewItem(item.Title());

                if (item.IsImportant())
                    listViewItem.ForeColor = Color.Red;

                listViewItem.SubItems.Add(item.TypeStr());
                listViewItem.SubItems.Add(item.Text());
                listViewItem.Tag = item;
                viewItemList.Add(listViewItem);
            }

            listView1.Items.Clear();

            listView1.Items.AddRange(viewItemList.ToArray());

            toolStripStatusLabel1.Text = string.Format("检测完成，共有 {0} 项更新。", listView1.Items.Count);

            menuStrip1.Enabled = true;
        }

        void UpdateInfo()
        {
            BeginInvoke(new Action(() =>
            {
                menuStrip1.Enabled = false;
            }));

            try
            {
                var helper = GetTsinghuaCourseInfoHelper();

                helper.ProcessLog += (s => toolStripStatusLabel1.Text = s);
                helper.Login();
                helper.GetCourseList();
                helper.GetCoursesInfo();

                ShowMainPage();

                var itemOnShow = GetItemDataList(helper);

                BeginInvoke(new Action(() => UpdateInfo_MainThread(itemOnShow)));
            }
            catch (NoLoginIn) { }
        }

        void ThreadUpdateInfo()
        {
            var thread = new Thread(
                o =>
                    {
                        try
                        {
                            UpdateInfo();
                        }
                        catch (Exception e)
                        {
                            BeginInvoke(
                                new Action(
                                    () =>
                                        {
                                            toolStripStatusLabel1.Text = string.Format("发生错误 {0}", e.Message);
                                            menuStrip1.Enabled = true;
                                        }
                                    )
                                );
                        }
                    }
                );

            thread.IsBackground = true;
            thread.Start();
        }

        void ShowMainPage()
        {
            webBrowser1.Navigate(_MainPageUrl);
        }

        private void webBrowser1_NewWindow(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            try
            {
                if (webBrowser1.Document != null && webBrowser1.Document.ActiveElement != null)
                {
                    var url = webBrowser1.Document.ActiveElement.GetAttribute("href");

                    webBrowser1.Navigate(url);
                }
            }
            catch { }
        }

        private void listView1_ItemActivate(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count <= 0)
                return;
            
            var urlString = ((ViewListItemData) listView1.SelectedItems[0].Tag).Url();
            webBrowser1.Navigate(urlString);
        }

        private void 重新检测ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ThreadUpdateInfo();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ThreadUpdateInfo();
        }

        private void 显示网络学堂主界面ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.SelectedItems.Clear();
            ShowMainPage();
        }

        public string HtmlDecode(string html)
        {
            var result = html;
            result = Regex.Replace(result, "^\\s*", "");
            result = Regex.Replace(result, "\\s*$", "");
            return HttpUtility.HtmlDecode(result);
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (!(listView1.SelectedItems[0].Tag is ViewListItemData_FileList))
                return;

            var fileList = (listView1.SelectedItems[0].Tag as ViewListItemData_FileList).FileList;

            if (webBrowser1.Document == null)
                return;
            
            var tableElem = webBrowser1.Document.GetElementById("Layer1");
            if (tableElem == null)
                return;

            var elseElem = tableElem.All.Cast<HtmlElement>()
                .Where(elem => elem.GetAttribute("classname").StartsWith("tr"))
                .Where(elem =>
                           {
                               var collection = elem.GetElementsByTagName("a");

                               string innerHtml;
                               try
                               {
                                   innerHtml = HtmlDecode(collection[0].Parent.Parent.InnerHtml);
                               }
                               catch
                               {
                                   return false;
                               }

                               Func<CourseFileInfo, bool> predicate =
                                   file => innerHtml.Contains(file.Title)
                                           && innerHtml.Contains(file.Description)
                                           && innerHtml.Contains(file.Date);

                               return !fileList.Any(predicate);
                           });

            foreach (var elem in elseElem)
                elem.Style += "; display:none";
        }
    }
}
