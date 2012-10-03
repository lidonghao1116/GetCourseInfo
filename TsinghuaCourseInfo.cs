using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using System.Diagnostics;

namespace GetCourseInfoV2
{
    [Serializable]
    abstract class CourseInfoItem : IEquatable<CourseInfoItem>
    {
        public CourseInfo Course;
        public string Url;
        public string Title;
        public bool IsImportant;

        public abstract string GetHashString();
        public abstract string ItemTypeStr();

        public string CourseName()
        {
            return Course.Title;
        }

        public bool Equals(CourseInfoItem other)
        {
            return GetHashString() == other.GetHashString();
        }

        public override int GetHashCode()
        {
            return GetHashString().GetHashCode();
        }

        public static string HtmlDecode(string html)
        {
            var result = html;
            result = Regex.Replace(result, "^\\s+", "");
            result = Regex.Replace(result, "\\s+$", "");
            return HttpUtility.HtmlDecode(result);
        }
    }

    [DebuggerDisplayAttribute("{Title}")]
    [Serializable]
    sealed class CourseNote : CourseInfoItem
    {
        public string Publisher;
        public string Date;

        public override string ItemTypeStr()
        {
            return "课程公告";
        }

        public override string GetHashString()
        {
            return Course.Title + Title + Publisher + Date;
        }

        public static CourseNote Parse(CourseInfo course,HtmlNode node)
        {
            var title = HtmlDecode(node.SelectSingleNode("./td[2]/a").InnerText);
            var url = node.SelectSingleNode("./td[2]/a").GetAttributeValue("href", "");
            var publisher = HtmlDecode(node.SelectSingleNode("./td[3]").InnerText);
            var date = node.SelectSingleNode("./td[4]").InnerText;
            var important = node.SelectSingleNode("./td[2]/a/font[@color=\"red\"]") != null;

            url = url.Replace("课程公告", "%E8%AF%BE%E7%A8%8B%E5%85%AC%E5%91%8A");
            url = "http://learn.tsinghua.edu.cn/MultiLanguage/public/bbs/" + url;

            return new CourseNote
                       {
                    Course = course,
                    Url = url,
                    Title = title,
                    Publisher = publisher,
                    Date = date,
                    IsImportant = important
                };
        }
    }

    [DebuggerDisplayAttribute("{Title}")]
    [Serializable]
    sealed class CourseFileInfo : CourseInfoItem
    {
        public string Description;
        public string Date;

        public override string ItemTypeStr()
        {
            return "课程文件";
        }

        public override string GetHashString()
        {
            return Course.Title + Title + Description + Date;
        }

        public static CourseFileInfo Parse(CourseInfo course, HtmlNode node)
        {
            var title = HtmlDecode(node.SelectSingleNode("./td[2]/a").InnerText);
            var url = "http://learn.tsinghua.edu.cn/MultiLanguage/lesson/student/download.jsp?course_id=" + course.ID;
            var description = HtmlDecode(node.SelectSingleNode("./td[3]").InnerText);
            var date = node.SelectSingleNode("./td[5]").InnerText;
            var important = node.SelectSingleNode("./td[2]/a/font[@color=\"red\"]") != null;

            return new CourseFileInfo
                       {
                           Course = course,
                           Url = url,
                           Title = title,
                           Description = description,
                           Date = date,
                           IsImportant = important
                       };
        }
    }

    [DebuggerDisplayAttribute("{Title}")]
    [Serializable]
    sealed class CourseHomework : CourseInfoItem
    {
        public string StartDate;
        public string Deadline;

        public override string ItemTypeStr()
        {
            return "课程作业";
        }

        public override string GetHashString()
        {
            return Course.Title + Title + StartDate + Deadline;
        }

        public static CourseHomework Parse(CourseInfo course, HtmlNode node)
        {
            var title = HtmlDecode(node.SelectSingleNode("./td[1]/a").InnerText);
            var url = "http://learn.tsinghua.edu.cn/MultiLanguage/lesson/student/" + node.SelectSingleNode("./td[1]/a").GetAttributeValue("href","");
            var startdate = node.SelectSingleNode("./td[2]").InnerText;
            var deadline = node.SelectSingleNode("./td[3]").InnerText;
            const bool important = true;

            return new CourseHomework
                       {
                           Course = course,
                           Url = url,
                           Title = title,
                           StartDate = startdate,
                           Deadline = deadline,
                           IsImportant = important
                       };
        }
    }

    [DebuggerDisplayAttribute("{Title}")]
    [Serializable]
    sealed class CourseDiscuss : CourseInfoItem
    {
        public string Publisher;
        public string ReplyCount;
        public string Date;

        public override string ItemTypeStr()
        {
            return "课程讨论";
        }

        public override string GetHashString()
        {
            return Course.Title + Title + Publisher + Date + ReplyCount;
        }

        public static CourseDiscuss Parse(CourseInfo course, HtmlNode node)
        {
            var title = HtmlDecode(node.SelectSingleNode("./td[1]/a").InnerText);
            var url = "http://learn.tsinghua.edu.cn/MultiLanguage/public/bbs/" + node.SelectSingleNode("./td[1]/a").GetAttributeValue("href", "");
            var publisher = HtmlDecode(node.SelectSingleNode("./td[2]").InnerText);
            var date = node.SelectSingleNode("./td[4]").InnerText;
            var replycount = node.SelectSingleNode("./td[3]").InnerText.Split('/')[0];
            const bool important = false;

            return new CourseDiscuss
                       {
                           Course = course,
                           Url = url,
                           Title = title,
                           Publisher = publisher,
                           ReplyCount = replycount,
                           Date = date,
                           IsImportant = important
                       };
        }
    }

    [DebuggerDisplayAttribute("{Title}")]
    [Serializable]
    sealed class CourseInfo
    {
        [NonSerialized]
        public TsinghuaCourseInfo CourseInfoHelper;

        public string Title;
        public string ID;
        public uint USHWCount;//UnsubmittedHomework

        public CourseNote[] CourseNoteList;
        public CourseFileInfo[] CourseFileInfoList;
        public CourseHomework[] CourseHomeworkList;
        public CourseDiscuss[] CourseDiscussList;

        public void GetCourseInfo()
        {
            GetCourseNoteList();
            GetCourseFileInfoList();
            GetCourseHomeworkList();
            GetCourseDiscussList();
        }

        void GetCourseNoteList()
        {
            var url = "https://learn.tsinghua.edu.cn/MultiLanguage/public/bbs/getnoteid_student.jsp?course_id=" + ID;
            var html = CourseInfoHelper.HttpHelper.HTTPGetTxt(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(".//table[@id=\"info_1\"]//table//tr[@class]");

            if(nodes != null)
            {
                CourseNoteList = nodes
                        .Select(node => CourseNote.Parse(this, node))
                        .ToArray();
            }
            else
            {
                CourseNoteList = new CourseNote[0];
            }
        }

        void GetCourseFileInfoList()
        {
            var url = "https://learn.tsinghua.edu.cn/MultiLanguage/lesson/student/download.jsp?course_id=" + ID;
            var html = CourseInfoHelper.HttpHelper.HTTPGetTxt(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(".//table[@id=\"table_box\"]//tr[@class]");

            if (nodes != null)
            {
                CourseFileInfoList = nodes
                        .Select(node => CourseFileInfo.Parse(this, node))
                        .ToArray();
            }
            else
            {
                CourseFileInfoList = new CourseFileInfo[0];
            }
        }

        void GetCourseHomeworkList()
        {
            var url = "https://learn.tsinghua.edu.cn/MultiLanguage/lesson/student/hom_wk_brw.jsp?course_id=" + ID;
            var html = CourseInfoHelper.HttpHelper.HTTPGetTxt(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(".//table[@id=\"info_1\"]//table//tr[@class]");

            if (nodes != null)
            {
                CourseHomeworkList = nodes
                        .Select(node => CourseHomework.Parse(this, node))
                        .ToArray();
            }
            else
            {
                CourseHomeworkList = new CourseHomework[0];
            }
        }

        public static string HtmlTrim(string html)
        {
            var result = html;
            result = Regex.Replace(result, "^\\s+", "");
            result = Regex.Replace(result, "\\s+$", "");
            return result;
        }

        void GetCourseDiscussList()
        {
            var url = "https://learn.tsinghua.edu.cn/MultiLanguage/public/bbs/gettalkid_student.jsp?course_id=" + ID;
            var html = CourseInfoHelper.HttpHelper.HTTPGetTxt(url);
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(".//table[@id=\"info_1\"]//table//table//tr[@class]");

            if (nodes != null)
            {
                CourseDiscussList = nodes
                        .Select(node => CourseDiscuss.Parse(this, node))
                        .ToArray();

                foreach (var discuss in CourseDiscussList)
                {
                    try
                    {
                        var discussHtml = CourseInfoHelper.HttpHelper.HTTPGetTxt(discuss.Url);
                        var discussDoc = new HtmlDocument();
                        discussDoc.LoadHtml(discussHtml);
                        var discussnodes = discussDoc.DocumentNode.SelectNodes(".//table[@id=\"info_1\"]//table[@id=\"table_box\"]//tr[1]//td[4]");
                        if(discussnodes != null)
                        {
                            var newDtStr = discussnodes.Select(node => HtmlTrim(node.InnerText)).Max();
                            discuss.Date = newDtStr;
                        }
                    }
                    catch { }
                }
            }
            else
            {
                CourseDiscussList = new CourseDiscuss[0];
            }
        }

        public static CourseInfo Parse(TsinghuaCourseInfo helper, HtmlNode node)
        {
            var title = CourseInfoItem.HtmlDecode(node.SelectSingleNode("./td[1]/a").InnerText);
            title = Regex.Replace(title, "\\([^()]*\\)$", "");

            var ushwcount = uint.Parse(node.SelectSingleNode("./td[2]/span").InnerText);

            var id = node.SelectSingleNode("./td[1]/a").GetAttributeValue("href", "").Split('=')[1];

            return new CourseInfo
                       {
                           CourseInfoHelper = helper,
                           ID = id,
                           Title = title,
                           USHWCount = ushwcount
                       };
        }
    }

    class TsinghuaCourseInfo
    {
        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool InternetSetCookie(string lpszUrlName, string lbszCookieName, string lpszCookieData);

        [DllImport("kernel32.dll")]
        public static extern Int32 GetLastError();

        string _userId;
        string _userPassword;
        public HTTPHelper HttpHelper;
        public CourseInfo[] CourseList;
        public delegate void ProcessLogDelegate(string str);
        public ProcessLogDelegate ProcessLog;

        CourseInfoItem[] _oldItemList;
        CourseInfoItem[] _allItemList;

        public TsinghuaCourseInfo(string userID, string userPassword)
        {
            HttpHelper = new HTTPHelper(10000);
            _userId = userID;
            _userPassword = userPassword;
        }

        public void Login()
        {
            var postStr = string.Format("userid={0}&userpass={1}&submit1=%B5%C7%C2%BC", _userId, _userPassword);
            ProcessLog("正在登录 网络学堂...");

            const string postUrl = "https://learn.tsinghua.edu.cn/MultiLanguage/lesson/teacher/loginteacher.jsp";
            HttpHelper.HTTPPostTxt(postUrl, postStr);

            var html = HttpHelper.HTTPPostTxt("https://learn.tsinghua.edu.cn/MultiLanguage/lesson/student/mainstudent.jsp", postStr);

            if (!html.Contains("MyCourse.jsp?language=cn"))
                throw new Exception("登录错误，请重新检查用户名和密码。");

            const string webSiteUrl = "https://learn.tsinghua.edu.cn/";

            var cookies = HttpHelper._cookieContainer.GetCookies(new Uri(webSiteUrl));
            
            foreach (var cookie in cookies)
            {
                var cookieStr = cookie.ToString();
                var index = cookieStr.IndexOf('=');
                var cookieName = cookieStr.Substring(0, index);
                var cookieData = cookieStr.Substring(index + 1);
                InternetSetCookie(webSiteUrl, cookieName, cookieData);
            }
        }

        public void GetCourseList()
        {
            ProcessLog("正在获取 课程列表");
            const string url = "https://learn.tsinghua.edu.cn/MultiLanguage/lesson/student/MyCourse.jsp?typepage=1";
            var html = HttpHelper.HTTPGetTxt(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(".//table[@id=\"info_1\"]//tr[@class]");

            if (nodes != null)
            {
                CourseList = nodes
                    .Select(node => CourseInfo.Parse(this, node))
                    .ToArray();
            }
            else
            {
                CourseList = new CourseInfo[0];
            }
        }

        public void GetCoursesInfo()
        {
            foreach (var course in CourseList)
            {
                ProcessLog(string.Format("正在获取 课程 [{0}] 的信息...", course.Title));
                course.GetCourseInfo();
            }
        }

        void GetAllItems()
        {
            var itemList = new List<CourseInfoItem>();

            foreach (var course in CourseList)
            {
                itemList.AddRange(course.CourseNoteList);
                itemList.AddRange(course.CourseFileInfoList);
                itemList.AddRange(course.CourseHomeworkList);
                itemList.AddRange(course.CourseDiscussList);
            }

            _allItemList = itemList.ToArray();
        }

        public CourseInfoItem[] GetNewItems()
        {
            GetAllItems();
            SaveItemToFile();

            if (_oldItemList == null)
                return _allItemList;
            
            return _allItemList.Except(_oldItemList).ToArray();
        }

        const string DataFileName = "userdata.dat";

        public static TsinghuaCourseInfo LoadFromDataFile()
        {
            var item = new TsinghuaCourseInfo(null, null);
            try
            {
                using (var fs = new FileStream(DataFileName, FileMode.Open))
                {
                    var randBytesLength = fs.ReadByte();
                    randBytesLength *= randBytesLength;

                    fs.Seek(randBytesLength, SeekOrigin.Current);

                    var rand = new Random(randBytesLength);

                    using (var mstream = new MemoryStream())
                    {
                        var buffer = new byte[1024];
                        while (true)
                        {
                            var len = fs.Read(buffer, 0, buffer.Length);

                            buffer = buffer.Select(b => (byte)(b ^ (byte)rand.Next(256))).ToArray();

                            mstream.Write(buffer, 0, len);

                            if (len < buffer.Length)
                                break;
                        }

                        mstream.Seek(0, SeekOrigin.Begin);

                        using (var stream = new GZipStream(mstream, CompressionMode.Decompress))
                        {
                            var formatter = new BinaryFormatter();
                            item._userId = (string)formatter.Deserialize(stream);
                            item._userPassword = (string)formatter.Deserialize(stream);
                            item._oldItemList = (CourseInfoItem[])formatter.Deserialize(stream);
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
            return item;
        }

        public void SaveItemToFile()
        {
            using (var fs = new FileStream(DataFileName, FileMode.Create))
            {
                //使用固定种子，这样前面的随机数据不再因每次运行而改变
                var rand = new Random(_userId.GetHashCode());

                //随机长度
                var randBytesLength = rand.Next(32, 64);
                fs.WriteByte((byte)randBytesLength);
                randBytesLength *= randBytesLength;

                var array = new byte[randBytesLength];
                rand.NextBytes(array);

                fs.Write(array, 0, randBytesLength);

                rand = new Random(randBytesLength);
                using (var mstream = new MemoryStream())
                {
                    using (var stream = new GZipStream(mstream, CompressionMode.Compress))
                    {
                        var formatter = new BinaryFormatter();
                        formatter.Serialize(stream, _userId);
                        formatter.Serialize(stream, _userPassword);
                        formatter.Serialize(stream, _allItemList);
                    }

                    var transformData = mstream.ToArray()
                        .Select(b => (byte)(b ^ (byte)rand.Next(256)))
                        .ToArray();

                    fs.Write(transformData, 0, transformData.Length);
                }
            }
        }
    }


    abstract class ViewListItemData : IComparable<ViewListItemData>
    {
        public abstract string Title();
        public abstract string Url();
        public abstract int TypeIndex();
        public abstract string TypeStr();
        public abstract string Text();
        public abstract bool IsImportant();
        public abstract string TextDate();
        public abstract string CourseID();

        public int CompareTo(ViewListItemData other)
        {
            var result = Title().CompareTo(other.Title());
            if (result != 0)
                return result;

            result = TypeIndex().CompareTo(other.TypeIndex());
            if (result != 0)
                return result;

            return -(TextDate().CompareTo(other.TextDate()));
        }
    }

    sealed class ViewListItemData_Normal : ViewListItemData
    {
        public CourseInfoItem Item;

        public ViewListItemData_Normal(CourseInfoItem item)
        {
            Item = item;
        }

        public override string Title()
        {
            return Item.CourseName();
        }

        public override string Url()
        {
            return Item.Url;
        }

        public override int TypeIndex()
        {
            if (Item is CourseNote)
                return 1;
            
            if (Item is CourseHomework)
                return 3;
            
            // if (Item is CourseDiscuss)
            return 4;
        }

        public override string TypeStr()
        {
            return Item.ItemTypeStr();
        }

        public override string Text()
        {
            return Item.Title;
        }

        public override bool IsImportant()
        {
            return Item.IsImportant;
        }

        public override string TextDate()
        {
            if (Item is CourseNote)
                return (Item as CourseNote).Date;
            
            if (Item is CourseHomework)
                return (Item as CourseHomework).StartDate;
            
            if (Item is CourseDiscuss)
                return (Item as CourseDiscuss).Date;

            return "";
        }

        public override string CourseID()
        {
            return Item.Course.ID;
        }
    }

    sealed class ViewListItemData_FileList : ViewListItemData
    {
        public CourseFileInfo[] FileList;

        public ViewListItemData_FileList(CourseFileInfo[] fileList)
        {
            FileList = fileList;
        }

        public override string Title()
        {
            return FileList[0].CourseName();
        }

        public override string Url()
        {
            return FileList[0].Url;
        }

        public override int TypeIndex()
        {
            return 2;
        }

        public override string TypeStr()
        {
            return FileList[0].ItemTypeStr();
        }

        public override string Text()
        {
            return string.Format("有 {0} 个文件更新。", FileList.Length);
        }

        public override bool IsImportant()
        {
            return FileList.Any(f => f.IsImportant);
        }

        public override string TextDate()
        {
            return FileList.Select(f => f.Date).Max();
        }

        public override string CourseID()
        {
            return FileList[0].Course.ID;
        }
    }
}
