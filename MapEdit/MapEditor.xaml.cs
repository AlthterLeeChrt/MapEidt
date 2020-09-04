using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Xml.XPath;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;
using System.Diagnostics;

namespace MapEdit
{
    /// <summary>
    /// MapEditor.xaml 的交互逻辑
    /// </summary>
    public partial class MapEditor : UserControl
    {
        public MapEditor()
        {
            InitializeComponent();
        }
        #region 类合集
        public class agvs
        {
            public int i_id;
            public int i_xPosition;
            public int i_yPosition;
            public int or;
            public double d_width;
            public double d_length;
            public double d_radius;
            public double d_maxSpeed;
            public double d_minSpeed;
        }
        public class waypoints
        {
            public int i_xPosition;
            public int i_yPosition;
            public int i_id;        //路径点id
            public int delete = 0;  //删除标志  1:删除 0:不删除
            public int used = 0;    //被引用次数标志
        }
        public class roads
        {
            public int i_id;        //路线id
            public int i_p1;
            public int i_p2;
            public int i_through;       //0：禁止通行  1：p1到p2  2：p2到p1  3：双向通行
            public int i_LorA = 1;      //直线、弧线标记  1:直线  2:顺时针弧线  3:逆时针弧线
            public int delete = 0;
            public waypoints wayP1 = new waypoints();
            public waypoints wayP2 = new waypoints();
        }
        public class sites
        {
            public int i_id;        //工作点id
            public int i_rid;       //路径点id
            public int hold = 1;        //是否锁住road，默认1
            public int delete = 0;
            public int i_xPosition;
            public int i_yPosition;
            public double d_or;     //偏移角度
        }
        #endregion
        #region 全局变量
        int count_Waypoint = 0;      //统计所有路径点
        int count_Road = 0;          //统计所有路线
        int count_Site = 0;          //统计所有站点
        int show_point;             //删除数量标记
        int show_road;
        int show_site;
        int count_DrawHand = 0;         //统计手绘路径点数量，判断是否为偶数个
        int flag_PoDrawHand = 0;        //手动绘制路径点起始标记
        //获取id标记
        int flag_poId = 0;
        int flag_roId = 0;
        int flag_siteId = 0;
        int flag_p = 1;                 //连接路径数量标记
        //更改前的数据（被鼠标选中时id）
        int old_poId = 0;
        int old_roId = 0;
        int old_siId = 0;
        int old_through = 0;
        int old_hold = 0;
        int model = 1;                  //应用模式  1:预览模式（工具栏和属性栏隐藏）  0:编辑模式
        int fenceY = 0;
        int originX = 0;                //初始坐标X轴
        int originY = 0;             //初始坐标Y轴
        int totalX = 0;                 //总移动变量
        int totalY = 0;
        string xmlAddress;              //文件路径
        double scaleLe = 1;             //缩放系数
        double height;                  //变换尺寸
        double width;
        double fenceX = 0;              //视野边界
        bool isDrawPoint = false;       //手动绘制路径点
        bool isDrawLine = false;        //手动绘制直线
        bool isDrawArc = false;         //手动绘制弧线
        bool isDrawSite = false;        //手动绘制站点
        bool isLink = false;            //连线标识
        bool isFileSaved = true;       //文件是否保存标识
        bool deleteElement = false;     //删除路径点标识
        //声明地图对象
        public List<waypoints> way_point = new List<waypoints>();
        public Dictionary<int, TextBox> txtPinfo = new Dictionary<int, TextBox>();
        public List<roads> road = new List<roads>();
        public Dictionary<int, TextBox> txtRinfo = new Dictionary<int, TextBox>();
        public List<sites> site = new List<sites>();
        public Dictionary<int, TextBox> txtSinfo = new Dictionary<int, TextBox>();

        public Dictionary<int, agvs> agvdic = new Dictionary<int, agvs>();
        public Dictionary<int, Ellipse> agvele = new Dictionary<int, Ellipse>();
        public Dictionary<int, TextBox> txtagvid = new Dictionary<int, TextBox>();
        public ConcurrentQueue<agvs> agvque = new ConcurrentQueue<agvs>();      //agv位置更新队列
        DispatcherTimer agvTime = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };  //agv位置更新时间片
        //鼠标缩放时坐标 
        Point startScalePosition = new Point();
        Point endScalePosition = new Point();
        Rectangle showEle;          //元素选择标识
        XmlDocument xmlDoc = new XmlDocument();     //xml文档
        XmlDocument xmlConfig = new XmlDocument();  //配置文件
        string xmlName;

        //连线坐标点
        readonly waypoints p1 = new waypoints();
        readonly waypoints p2 = new waypoints();

        readonly ScaleTransform totalCanvasScale = new ScaleTransform();
        int agvid = 1001, agvx = 1000, agvy = 5000;
        #endregion
        #region 读取数据
        //界面加载
        private void MapEditLoad(object sender, RoutedEventArgs e)
        {
            agvTime.Tick += (s, eve) =>
            {
                agvs agv;
                while (agvque.TryDequeue(out agv))
                {
                    AgvLocation(agv.i_id, agv.i_xPosition, agv.i_yPosition, agv.or);
                }
                //test();
            };
            agvTime.Start();
            canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            canvas.MouseRightButtonDown += Canvas_MouseRightButtonDown;
            canvas.MouseRightButtonUp += Canvas_MouseRightButtonUp;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseWheel += Canvas_MouseWheel;
            btnOpenMap.ToolTip = $"{"OpenMap"}";
            btnOpenXml.ToolTip = $"{"OpenXml"}";
            btnExPort.ToolTip = $"{"ExPort"}";
            btnDelete.ToolTip = $"{"Delete"}";
            btnPreview.ToolTip = $"{"Preview"}";
            btnDrawPo.ToolTip = $"{"DrawPoint"}";
            btnDrawLine.ToolTip = $"{"DrawLine"}";
            btnDrawArc.ToolTip = $"{"DrawArc"}";
            btnDrawSite.ToolTip = $"{"DrawSite"}";
            btnLink.ToolTip = $"{"Link"}";
            Panel.SetZIndex(LocalName, 21);
            ModelView();
        }
        private void test()
        {
            Random ra = new Random();
            for (int i = 1; i < 25; i++)
            {
                agvx = ra.Next(1000, 100000);
                agvy = ra.Next(1000, 60000);
                UpdateAGV(i, agvx, agvy, 0);
            }
        }
        public void OpenMap()
        {
            Dispatcher.Invoke(() =>
            {
                isDrawLine = false;
                isDrawArc = false;
                OpenFileDialog openMap = new OpenFileDialog
                {
                    Filter = "JPG files(*.jpg)|*.jpg|JPEG files(*.jpeg)|*.jpeg|" +
                    "PNG files(*.png)|*.png|All files(*.)|*.*"
                };
                openMap.ShowDialog();
                ImageBrush brushImage = new ImageBrush();
                if (openMap.FileName != "")
                {
                    try
                    {
                        brushImage.ImageSource = new BitmapImage(new Uri(openMap.FileName.ToString()));
                        brushImage.Stretch = Stretch.Fill;
                        canvas.Background = brushImage;
                        Canvas.SetLeft(canvas, 0);
                        Canvas.SetTop(canvas, 0);
                    }
                    catch (Exception)
                    {

                    }
                }
            });
        }
        public void OpenXml(string map = null)
        {
            isDrawLine = false;
            isDrawArc = false;
            if (!isFileSaved)
            {
                MessageBox.Show("请选择当前文件保存路径");
                Save();
            }
            if (null == map)
            {
                OpenFileDialog openXml = new OpenFileDialog
                {
                    Filter = "XML files(*.xml)|*.xml|All files(*.)|*.*"
                };
                openXml.ShowDialog();
                map = openXml.FileName.ToString();
                xmlAddress = map;
                xmlName = openXml.SafeFileName;     //获取原文件名
                LocalName.Content = "文件:" + map.ToString();
            }
            Clear();
            if (!canvas.Children.Contains(labPos))
            {
                canvas.Children.Add(baseGrid);
            }
            try
            {
                xmlDoc.Load(map);
                XmlElement rootEle = xmlDoc.DocumentElement;    //获取根节点
                XmlNodeList wayEle = rootEle.GetElementsByTagName("waypoint");      //路径点子节点
                XmlNodeList roadEle = rootEle.GetElementsByTagName("road");        //路线子节点
                XmlNodeList siteEle = rootEle.GetElementsByTagName("site");        //工作站点子节点
                                                                                   //读取路径点数据
                foreach (XmlNode waynode in wayEle)
                {
                    way_point.Add(new waypoints()
                    {
                        i_id = int.Parse(((XmlElement)waynode).GetAttribute("id")),
                        i_xPosition = int.Parse(((XmlElement)waynode).GetAttribute("x")),
                        i_yPosition = int.Parse(((XmlElement)waynode).GetAttribute("y"))
                    });
                    if (way_point[count_Waypoint].i_id > flag_poId)
                    {
                        flag_poId = way_point[count_Waypoint].i_id;     //获取id最大值,作为手动路径点id起始标记
                    }
                    count_Waypoint++;       //统计xml中路径点数量
                }
                //读取路径数据
                foreach (XmlNode roadnode in roadEle)
                {
                    road.Add(new roads()
                    {
                        i_id = int.Parse(((XmlElement)roadnode).GetAttribute("id")),
                        i_through = int.Parse(((XmlElement)roadnode).GetAttribute("through")),
                        i_p1 = int.Parse(((XmlElement)roadnode).GetAttribute("p1")),
                        i_p2 = int.Parse(((XmlElement)roadnode).GetAttribute("p2"))
                    });
                    if (road[count_Road].i_id > flag_roId)
                    {
                        flag_roId = road[count_Road].i_id;
                    }
                    count_Road++;           //统计xml中路径数量
                }
                //读取站点数据
                foreach (XmlNode sitenode in siteEle)
                {
                    site.Add(new sites()
                    {
                        i_id = int.Parse(((XmlElement)sitenode).GetAttribute("id")),
                        i_xPosition = int.Parse(((XmlElement)sitenode).GetAttribute("x")),
                        i_yPosition = int.Parse(((XmlElement)sitenode).GetAttribute("y")),
                        d_or = int.Parse(((XmlElement)sitenode).GetAttribute("or")),
                        i_rid = int.Parse(((XmlElement)sitenode).GetAttribute("rid")),
                        hold = int.Parse(((XmlElement)sitenode).GetAttribute("hold"))
                    });
                    if (site[count_Site].i_id > flag_siteId)
                    {
                        flag_siteId = site[count_Site].i_id;        //获取xml中id最大值
                    }
                    count_Site++;           //统计xml中站点数量
                }
                countPoints();
                Horizon();
            }
            catch (Exception)
            {
                MessageBox.Show("未能正确获取xml文件");
                pointNum.Content = "0";
                roadNum.Content = "0";
                siteNum.Content = "0";
            }
            flag_PoDrawHand = count_Waypoint;
        }
        #endregion
        #region 鼠标事件
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point startPo = e.GetPosition(canvas);
            Canvas curCanvas = new Canvas();
            // 多边形，起点 --> 终点 --> 第三点 --> 第四点 --> 终点
            Polygon myPolygon = new Polygon
            {
                Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 255, 60)),
                StrokeThickness = 1,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = new SolidColorBrush(Color.FromArgb(255, 0, 255, 60)),
                Points = new PointCollection
                {
                    startPo,
                    startPo,
                    startPo,
                    startPo,
                    startPo,
                },
            };
            curCanvas.Children.Add(myPolygon);
            canvas.Children.Add(curCanvas);


            Point wPoint = e.GetPosition(canvas);
            if (isDrawPoint)
            {
                flag_poId++;
                way_point.Add(new waypoints()
                {
                    i_xPosition = (int)(wPoint.X * 100),
                    i_yPosition = (int)((height - wPoint.Y) * 100),
                    i_id = flag_poId    //手动id
                });
                drawPoint(flag_poId, (int)wPoint.X, (int)(height - wPoint.Y));
                isFileSaved = false;
                pTxtId.Text = way_point[count_Waypoint].i_id.ToString();
                pTxtX.Text = way_point[count_Waypoint].i_xPosition.ToString();
                pTxtY.Text = way_point[count_Waypoint].i_yPosition.ToString();
                count_Waypoint++;
                pointNum.Content = count_Waypoint.ToString();
            }
            if (isDrawLine || isDrawArc)
            {
                try
                {
                    flag_poId++;
                    //手绘路径点存储
                    way_point.Add(new waypoints()
                    {
                        i_xPosition = (int)(wPoint.X * 100),
                        i_yPosition = (int)((height - wPoint.Y) * 100),
                        i_id = flag_poId    //手动id
                    });
                    drawPoint(flag_poId, (int)wPoint.X, (int)(height - wPoint.Y));
                    isFileSaved = false;
                    //属性面板显示
                    pTxtId.Text = way_point[count_Waypoint].i_id.ToString();
                    pTxtX.Text = way_point[count_Waypoint].i_xPosition.ToString();
                    pTxtY.Text = way_point[count_Waypoint].i_yPosition.ToString();
                    count_Waypoint++;       //手动路径点
                    count_DrawHand++;       //手动路径点统计
                    pointNum.Content = count_Waypoint.ToString();
                }
                catch (Exception)
                {

                }
                //手动绘制路径
                Drawing();
            }
            //手动绘制站点
            if (isDrawSite)
            {
                flag_siteId++;
                site.Add(new sites()
                {
                    i_xPosition = (int)(wPoint.X * 100),
                    i_yPosition = (int)((height - wPoint.Y) * 100),
                    i_id = flag_siteId
                });
                drawSite(flag_siteId, wPoint.X, height - wPoint.Y);
                isFileSaved = false;
                //显示属性
                sTxtId.Text = site[count_Site].i_id.ToString();
                sTxtX.Text = site[count_Site].i_xPosition.ToString();
                sTxtY.Text = site[count_Site].i_yPosition.ToString();
                sTxtOr.Text = site[count_Site].d_or.ToString();
                sTxtRoadId.Text = site[count_Site].i_rid.ToString();
                sTxtHold.Text = site[count_Site].hold.ToString();
                count_Site++;
                siteNum.Content = count_Site.ToString();
            }
            //连线
            if (isLink)
            {
                //遍历找出需要连接的路径点
                for (int i = 0; i < count_Waypoint; i++)
                {
                    int x = (int)Math.Abs(wPoint.X * 100 - way_point[i].i_xPosition);
                    int y = (int)Math.Abs((height - wPoint.Y) * 100 - way_point[i].i_yPosition);
                    if (x < 500 && y < 500 && flag_p % 2 != 0)
                    {
                        p1.i_xPosition = way_point[i].i_xPosition;
                        p1.i_yPosition = way_point[i].i_yPosition;
                        p1.i_id = way_point[i].i_id;
                        way_point[i].used++;
                        break;
                    }
                }
                for (int i = 0; i < count_Waypoint; i++)
                {
                    int x = (int)Math.Abs(wPoint.X * 100 - way_point[i].i_xPosition);
                    int y = (int)Math.Abs((height - wPoint.Y) * 100 - way_point[i].i_yPosition);
                    if (x < 500 && y < 500 && flag_p % 2 == 0)
                    {
                        p2.i_xPosition = way_point[i].i_xPosition;
                        p2.i_yPosition = way_point[i].i_yPosition;
                        p2.i_id = way_point[i].i_id;
                        way_point[i].used++;
                        Link();
                        isFileSaved = false;
                    }
                }
                flag_p++;
            }
        }
        //路径点选择事件
        private void Point_MouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            if (canvas.Children.Contains(showEle))
            {
                canvas.Children.Remove(showEle);
            }
            Ellipse ell = (Ellipse)sender;
            int id = (int)ell.Tag;
            for (int i = 0; i < count_Waypoint; i++)
            {
                if (id == way_point[i].i_id)
                {
                    if (deleteElement)
                    {
                        way_point[i].delete = 1;
                        way_point[i].used = 0;
                        canvas.Children.Remove(ell);
                        DeletePoint(way_point[i]);
                    }
                    else
                    {
                        old_poId = way_point[i].i_id;
                        pTxtId.Text = way_point[i].i_id.ToString();
                        pTxtX.Text = (way_point[i].i_xPosition - originX * 100).ToString();
                        pTxtY.Text = (way_point[i].i_yPosition - originY * 100).ToString();
                        showElement(way_point[i].i_xPosition / 100, way_point[i].i_yPosition / 100);
                    }
                }
            }
            PointGrid();
        }
        private void PointGrid()
        {
            //属性面板切换
            Tree.IsExpanded = true;
            trPoint.IsExpanded = true;
            trSite.IsExpanded = false;
            trRoad.IsExpanded = false;
        }
        //站点选择事件
        private void Site_MouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            if (canvas.Children.Contains(showEle))
            {
                canvas.Children.Remove(showEle);
            }
            Rectangle rec = (Rectangle)sender;
            int id = (int)rec.Tag;
            Tree.IsExpanded = true;
            trRoad.IsExpanded = false;
            trSite.IsExpanded = true;
            trRoad.IsExpanded = false;
            for (int i = 0; i < count_Site; i++)
            {
                if (id == site[i].i_id)
                {
                    if (deleteElement)
                    {
                        canvas.Children.Remove(rec);
                        site[i].delete = 1;
                    }
                    else
                    {
                        old_siId = site[i].i_id;
                        old_hold = site[i].hold;
                        sTxtId.Text = site[i].i_id.ToString();
                        sTxtX.Text = (site[i].i_xPosition - originX * 100).ToString();
                        sTxtY.Text = (site[i].i_yPosition - originY * 100).ToString();
                        sTxtOr.Text = site[i].d_or.ToString();
                        sTxtRoadId.Text = site[i].i_rid.ToString();
                        sTxtHold.Text = site[i].hold.ToString();
                        showElement(site[i].i_xPosition / 100, site[i].i_yPosition / 100);
                    }
                }
            }
            SiteGrid();
        }
        private void SiteGrid()
        {
            //属性面板切换
            Tree.IsExpanded = true;
            trPoint.IsExpanded = false;
            trSite.IsExpanded = true;
            trRoad.IsExpanded = false;
        }
        //路径选择事件
        private void Road_MouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            if (canvas.Children.Contains(showEle))
            {
                canvas.Children.Remove(showEle);
            }
            Path path = (Path)sender;
            int id = (int)path.Tag;
            Tree.IsExpanded = true;
            trPoint.IsExpanded = false;
            trSite.IsExpanded = false;
            trRoad.IsExpanded = true;
            for (int i = 0; i < count_Road; i++)
            {
                if (road[i].i_id == id)
                {
                    if (deleteElement)
                    {
                        road[i].delete = 1;
                        canvas.Children.Remove(path);
                        DeleteRoad(road[i]);
                    }
                    else
                    {
                        old_roId = road[i].i_id;
                        old_through = road[i].i_through;
                        rTxtId.Text = road[i].i_id.ToString();
                        rTxtStartX.Text = (road[i].wayP1.i_xPosition - originX * 100).ToString();
                        rTxtStartY.Text = (road[i].wayP1.i_yPosition - originY * 100).ToString();
                        rTxtEndX.Text = (road[i].wayP2.i_xPosition - originX * 100).ToString();
                        rTxtEndY.Text = (road[i].wayP2.i_yPosition - originY * 100).ToString();
                        rTxtThrough.Text = road[i].i_through.ToString();
                        rTxtStartId.Text = road[i].i_p1.ToString();
                        rTxtEndId.Text = road[i].i_p2.ToString();
                    }
                }
            }
            RoadGrid();
        }
        private void RoadGrid()
        {
            Tree.IsExpanded = true;
            trPoint.IsExpanded = false;
            trSite.IsExpanded = false;
            trRoad.IsExpanded = true;
        }
        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            canvas.ReleaseMouseCapture();
        }
        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            startScalePosition = e.GetPosition(canvas);    //变换起点
        }
        private void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            endScalePosition = e.GetPosition(canvas); //变换终点
            totalX = (int)((endScalePosition.X - startScalePosition.X) / scaleLe);
            totalY = (int)((startScalePosition.Y - endScalePosition.Y) / scaleLe);
            originX += totalX;       //初始坐标系
            originY += totalY;
            EleMove(totalX, totalY);
        }
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point position = e.GetPosition(canvas);
            MouseCatch(position);
            Point curPoint = e.GetPosition(canvas);
            // 鼠标处于按下状态
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DrawArrow(curPoint);
            }
            //鼠标实时坐标
            try
            {
                int x = (int)(position.X * 100);
                int y = (int)((height - position.Y) * 100);
                labPos.Content = x.ToString() + " , " + y.ToString();
                baseGrid.MouseEnter += baseGrid_MouseEnter;
                baseGrid.MouseLeave += baseGrid_MouseLeave;
            }
            catch (Exception)
            {

            }
        }
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (scaleLe < 0.6)
            {
                scaleLe = 0.6;
            }
            else if (scaleLe > 2.6)
            {
                scaleLe = 2.6;
            }
            Point scaleCenter = e.GetPosition(canvas);      //缩放中心坐标
            if (e.Delta > 0)
            {
                scaleLe *= 1.04;
            }
            else
            {
                scaleLe /= 1.04;
            }
            AdjustGraph(canvas, scaleCenter, scaleLe);
            labSca.Content = Math.Round(scaleLe, 5).ToString();
        }
        //地图缩放
        private void AdjustGraph(Canvas can, Point pCenter, double scal)
        {
            TransformGroup tGroup = new TransformGroup();
            //背景图缩放
            totalCanvasScale.ScaleX = scal;
            totalCanvasScale.ScaleY = scal;
            totalCanvasScale.CenterX = pCenter.X;
            totalCanvasScale.CenterY = pCenter.Y;
            tGroup.Children.Add(totalCanvasScale);
            try
            {
                can.RenderTransform = tGroup;
            }
            catch (Exception)
            {

            }
        }
        #endregion
        #region 绘制元素
        //绘制直线、弧线
        private void Drawing()
        {
            //手动绘制路径
            if (count_DrawHand % 2 == 0)
            {
                int x1, y1, x2, y2;
                for (int i = flag_PoDrawHand; i < count_Waypoint; i += 2)
                {
                    x1 = way_point[i].i_xPosition / 100;
                    y1 = way_point[i].i_yPosition / 100;
                    x2 = way_point[i + 1].i_xPosition / 100;
                    y2 = way_point[i + 1].i_yPosition / 100;
                    flag_roId++;
                    road.Add(new roads()
                    {
                        i_p1 = way_point[i].i_id,       //路径两端点id
                        i_p2 = way_point[i + 1].i_id,
                        wayP1 = way_point[i],
                        wayP2 = way_point[i + 1],
                        i_id = flag_roId
                    });
                    way_point[i].used++;            //路径点引用标记增加
                    way_point[i + 1].used++;
                    if (isDrawLine)
                    {
                        drawLine(count_Road, flag_roId, x1, y1, x2, y2);
                    }
                    if (isDrawArc)
                    {
                        drawArc(count_Road, flag_roId, x1, y1, x2, y2);
                    }
                    //属性面板显示
                    rTxtId.Text = road[count_Road].i_id.ToString();
                    rTxtStartX.Text = way_point[i].i_xPosition.ToString();
                    rTxtStartY.Text = way_point[i].i_yPosition.ToString();
                    rTxtEndX.Text = way_point[i + 1].i_xPosition.ToString();
                    rTxtEndY.Text = way_point[i + 1].i_yPosition.ToString();
                    rTxtThrough.Text = road[count_Road].i_through.ToString();
                    rTxtStartId.Text = way_point[i].i_id.ToString();
                    rTxtEndId.Text = way_point[i + 1].i_id.ToString();
                    flag_PoDrawHand += 2;      //每新增一条直线，标记加2
                    count_Road++;
                    roadNum.Content = count_Road.ToString();
                }
                DrawDirect();
            }
        }
        //修改数据后重绘
        private void reDraw()
        {
            if (!canvas.Children.Contains(labPos))
            {
                canvas.Children.Add(baseGrid);
            }
            for (int i = 0; i < count_Waypoint; i++)
            {
                int x, y;
                int id;
                if (way_point[i].delete == 0 && way_point[i].used > 0)
                {
                    x = way_point[i].i_xPosition / 100;
                    y = way_point[i].i_yPosition / 100;
                    id = way_point[i].i_id;
                    drawPoint(id, x, y);
                }
            }
            for (int i = 0; i < count_Road; i++)
            {
                int x1, y1, x2, y2;
                int id;
                if (road[i].delete == 0)
                {
                    x1 = road[i].wayP1.i_xPosition / 100;
                    y1 = road[i].wayP1.i_yPosition / 100;
                    x2 = road[i].wayP2.i_xPosition / 100;
                    y2 = road[i].wayP2.i_yPosition / 100;
                    id = road[i].i_id;
                    if (road[i].i_LorA == 1)
                    {
                        drawLine(i, id, x1, y1, x2, y2);
                    }
                    else if (road[i].i_LorA == 2)
                    {
                        PathFigure myPathFigure = new PathFigure
                        {
                            StartPoint = new Point(x1, height - y1)
                        };
                        //计算弧线的半轴长度
                        int i_sizeX = (int)Math.Abs(x2 - x1);
                        int i_sizeY = (int)Math.Abs(y2 - y1);
                        int i_size = (int)(Math.Sqrt(Math.Pow(i_sizeX, 2) + Math.Pow(i_sizeY, 2)) * 0.707);
                        try
                        {
                            myPathFigure.Segments.Add(
                                            new ArcSegment(
                                            new Point(x2, height - y2),
                                            new Size(i_size, i_size),
                                            0,                          //椭圆X轴旋转
                                            false, /* IsLargeArc */     //圆弧是否大于180°
                                            SweepDirection.Clockwise,   //正角方向：Clockwise
                                            true /* IsStroked */ ));
                            /// 创建一个PathGeometry以包含该图
                            PathGeometry myPathGeometry = new PathGeometry();
                            myPathGeometry.Figures.Add(myPathFigure);
                            string col = combRoadCol.Text;
                            Color color = (Color)ColorConverter.ConvertFromString(col);
                            // 显示PathGeometry
                            Path myPath = new Path
                            {
                                Stroke = new SolidColorBrush(color),
                                StrokeThickness = 4,
                                Data = myPathGeometry,
                                Tag = road[i].i_id         //路径id
                            };
                            if (road[i].i_through == 0)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 0}";
                            }
                            else if (road[i].i_through == 1)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 1 + "\n" + "Startid:" + road[i].i_p1 + "\n" + "Endid:" + road[i].i_p2}";
                            }
                            else if (road[i].i_through == 2)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 2 + "\n" + "Startid:" + road[i].i_p2 + "\n" + "Endid:" + road[i].i_p1}";
                            }
                            else if (road[i].i_through == 3)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 3}";
                            }
                            Panel.SetZIndex(myPath, 2);
                            canvas.Children.Add(myPath);
                            myPath.MouseLeftButtonDown += Road_MouseLeftButtonDown;
                        }
                        catch (Exception)
                        {

                        }
                    }
                    else if (road[i].i_LorA == 3)
                    {
                        PathFigure myPathFigure = new PathFigure
                        {
                            StartPoint = new Point(x1, height - y1)
                        };
                        //计算弧线的半轴长度
                        int i_sizeX = Math.Abs(x2 - x1);
                        int i_sizeY = Math.Abs(y2 - y1);
                        int i_size = (int)(Math.Sqrt(Math.Pow(i_sizeX, 2) + Math.Pow(i_sizeY, 2)) * 0.707);
                        try
                        {
                            myPathFigure.Segments.Add(
                                            new ArcSegment(
                                            new Point(x2, height - y2),
                                            new Size(i_size, i_size),
                                            0,                          //椭圆X轴旋转
                                            false, /* IsLargeArc */     //圆弧是否大于180°
                                            SweepDirection.Counterclockwise,   //负角方向：Counterclockwise
                                            true /* IsStroked */ ));

                            /// 创建一个PathGeometry以包含该图
                            PathGeometry myPathGeometry = new PathGeometry();
                            myPathGeometry.Figures.Add(myPathFigure);
                            string col = combRoadCol.Text;
                            Color color = (Color)ColorConverter.ConvertFromString(col);
                            // 显示PathGeometry
                            Path myPath = new Path
                            {
                                Stroke = new SolidColorBrush(color),
                                StrokeThickness = 4,
                                Data = myPathGeometry,
                                Tag = road[i].i_id         //路径id
                            };
                            if (road[i].i_through == 0)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 0}";
                            }
                            else if (road[i].i_through == 1)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 1 + "\n" + "Startid:" + road[i].i_p1 + "\n" + "Endid:" + road[i].i_p2}";
                            }
                            else if (road[i].i_through == 2)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 2 + "\n" + "Startid:" + road[i].i_p2 + "\n" + "Endid:" + road[i].i_p1}";
                            }
                            else if (road[i].i_through == 3)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 3}";
                            }
                            Panel.SetZIndex(myPath, 2);
                            canvas.Children.Add(myPath);
                            myPath.MouseLeftButtonDown += Road_MouseLeftButtonDown;
                        }
                        catch (Exception)
                        {

                        }
                    }
                }
            }
            for (int i = 0; i < count_Site; i++)
            {
                int x, y;
                int id;
                if (site[i].delete == 0)
                {
                    x = site[i].i_xPosition / 100;
                    y = site[i].i_yPosition / 100;
                    id = site[i].i_id;
                    drawSite(id, x, y);
                }
            }
            DrawDirect();
        }
        #region 绘制路径
        private void drawLine(int count, int id, int x1, int y1, int x2, int y2)
        {
            PathFigure myPathFigure = new PathFigure();
            // 创建一个PathGeometry以包含该图
            PathGeometry myPathGeometry = new PathGeometry();
            // 显示PathGeometry
            Path myPath = new Path
            {
                Tag = id         //路径id
            };
            myPathFigure.StartPoint = new Point(x1, height - y1);
            string col = combRoadCol.Text;      //读取combobox中的颜色
            Color color = (Color)ColorConverter.ConvertFromString(col);
            try
            {
                myPathFigure.Segments.Add(
                    new LineSegment(
                        new Point(x2, height - y2),
                        true));
                myPathGeometry.Figures.Add(myPathFigure);
                myPath.Stroke = new SolidColorBrush(color);
                myPath.StrokeThickness = 4;
                myPath.Data = myPathGeometry;
                if (road[count].i_through == 0)
                {
                    myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 0}";
                }
                else if (road[count].i_through == 1)
                {
                    myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 1 + "\n" + "Startid:" + road[count].i_p1 + "\n" + "Endid:" + road[count].i_p2}";
                }
                else if (road[count].i_through == 2)
                {
                    myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 2 + "\n" + "Startid:" + road[count].i_p2 + "\n" + "Endid:" + road[count].i_p1}";
                }
                else if (road[count].i_through == 3)
                {
                    myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 3}";
                }
                Panel.SetZIndex(myPath, 2);
                canvas.Children.Add(myPath);
                road[count].i_LorA = 1;
                myPath.MouseLeftButtonDown += Road_MouseLeftButtonDown;
            }
            catch (Exception)
            {

            }
        }
        private void drawArc(int count, int id, int x1, int y1, int x2, int y2)
        {
            PathFigure myPathFigure = new PathFigure
            {
                StartPoint = new Point(x1, height - y1)
            };
            //计算弧线的半轴长度
            int i_sizeX = Math.Abs(x2 - x1);
            int i_sizeY = Math.Abs(y2 - y1);
            int i_size = (int)(Math.Sqrt(Math.Pow(i_sizeX, 2) + Math.Pow(i_sizeY, 2)) * 0.707);     //计算弧线尺寸
            try
            {
                if (radClockwise.IsChecked == true)
                {
                    myPathFigure.Segments.Add(
                                    new ArcSegment(
                                    new Point(x2, height - y2),
                                    new Size(i_size, i_size),
                                    0,                          //椭圆X轴旋转
                                    false, /* IsLargeArc */     //圆弧是否大于180°
                                    SweepDirection.Clockwise,   //正角方向：Clockwise
                                    true /* IsStroked */ ));
                    road[count].i_LorA = 2;
                }
                if (radCounter.IsChecked == true)
                {
                    myPathFigure.Segments.Add(
                                    new ArcSegment(
                                    new Point(x2, height - y2),
                                    new Size(i_size, i_size),
                                    0,                          //椭圆X轴旋转
                                    false, /* IsLargeArc */     //圆弧是否大于180°
                                    SweepDirection.Counterclockwise,   //负角方向：Counterclockwise
                                    true /* IsStroked */ ));
                    road[count].i_LorA = 3;
                }
                /// 创建一个PathGeometry以包含该图
                PathGeometry myPathGeometry = new PathGeometry();
                myPathGeometry.Figures.Add(myPathFigure);
                string col = combRoadCol.Text;
                Color color = (Color)ColorConverter.ConvertFromString(col);
                // 显示PathGeometry
                Path myPath = new Path
                {
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = 4,
                    Data = myPathGeometry,
                    Tag = id         //路径id
                };
                if (road[count].i_through == 0)
                {
                    myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 0}";
                }
                else if (road[count].i_through == 1)
                {
                    myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 1 + "\n" + "Startid:" + road[count].i_p1 + "\n" + "Endid:" + road[count].i_p2}";
                }
                else if (road[count].i_through == 2)
                {
                    myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 2 + "\n" + "Startid:" + road[count].i_p2 + "\n" + "Endid:" + road[count].i_p1}";
                }
                else if (road[count].i_through == 3)
                {
                    myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 3}";
                }
                Panel.SetZIndex(myPath, 2);
                canvas.Children.Add(myPath);
                myPath.MouseLeftButtonDown += Road_MouseLeftButtonDown;
            }
            catch (Exception)
            {

            }
        }
        #endregion
        #region 绘制路径点
        private void drawAGV(int id, int x, int y, double agvHei,
            double agvWid, double txtHei, double txtWid)
        {
            Ellipse myPoint = agvele[id];
            TextBox txtagv = txtagvid[id];
            double ax, ay, tx, ty;
            ax = x - agvWid / 2;
            ay = height - y - agvHei / 2;
            tx = x - txtWid / 2;
            ty = height - y - txtHei / 2;
            //声明路径点
            try
            {
                txtagv.Margin = new Thickness(tx, ty, 0, 0);
                Panel.SetZIndex(txtagv, 21);
                myPoint.Margin = new Thickness(ax, ay, 0, 0);
                myPoint.Stroke = Brushes.LimeGreen;
                Panel.SetZIndex(myPoint, 20);        //图层层级最高
                canvas.Children.Add(myPoint);
                canvas.Children.Add(txtagv);
                myPoint.ToolTip = $"{"id:" + id + "\n" + "X:" + x * 100 + "\n" + "Y:" + y * 100}";
            }
            catch (Exception)
            {

            }
        }
        private void drawPoint(int id, int x, int y)
        {
            Ellipse myPoint = new Ellipse
            {
                Height = 7.0,
                Width = 7.0,
                Tag = id,     //id
                StrokeThickness = 3.5
            };
            //声明路径点
            string col = combColor.Text;
            Color color = (Color)ColorConverter.ConvertFromString(col);
            try
            {
                myPoint.Margin = new Thickness(x - 3.5,
                    height - y - 3.5, 0, 0);
                myPoint.Stroke = new SolidColorBrush(color);
                Panel.SetZIndex(myPoint, 3);        //图层层级最高
                canvas.Children.Add(myPoint);
                myPoint.ToolTip = $"{"id:" + id + "\n" + "X:" + x * 100 + "\n" + "Y:" + y * 100}";
                trPoint.IsExpanded = true;
            }
            catch (Exception)
            {

            }
            myPoint.MouseLeftButtonDown += Point_MouseLeftButtonDown;
        }
        #endregion
        #region 绘制站点
        private void drawSite(int id, double x, double y)
        {
            Rectangle mySite = new Rectangle
            {
                Height = 6,
                Width = 6,
                Tag = id,     //下标
                StrokeThickness = 3
            };
            mySite.MouseLeftButtonDown += Site_MouseLeftButtonDown;
            string col = combSiCol.Text;
            Color color = (Color)ColorConverter.ConvertFromString(col);
            try
            {
                mySite.Margin = new Thickness(x - 3, height - y - 3, 0, 0);
                mySite.Stroke = new SolidColorBrush(color);
                canvas.Children.Add(mySite);
                mySite.ToolTip = $"{"id:" + id + "\n" + "X:" + x * 100 + "\n" + "Y:" + y * 100}";
                trSite.IsExpanded = true;
            }
            catch (Exception)
            {

            }
        }
        #endregion
        private void showElement(double x, double y)
        {
            showEle = new Rectangle
            {
                Height = 10,
                Width = 10,
                StrokeThickness = 0.5
            };
            try
            {
                showEle.Margin = new Thickness(x - 5, height - y - 5, 0, 0);
                showEle.Stroke = Brushes.Red;
                Panel.SetZIndex(showEle, 22);
                canvas.Children.Add(showEle);
            }
            catch (Exception)
            {

            }
        }
        #region 根据数据绘制元素
        private void countPoints()
        {
            //自动绘制路径点
            for (int i = 0; i < count_Waypoint; i++)
            {
                int x1, y1;
                x1 = way_point[i].i_xPosition / 100;
                y1 = way_point[i].i_yPosition / 100;
                drawPoint(way_point[i].i_id, x1, y1);
            }
            pointNum.Content = count_Waypoint.ToString();
            //自动绘制路径
            for (int i = 0; i < count_Road; i++)
            {
                int x1 = 0, y1 = 0, x2 = 0, y2 = 0;
                for (int j = 0; j < count_Waypoint; j++)
                {
                    //通过路径id遍历查找两端点
                    if (road[i].i_p1 == way_point[j].i_id)
                    {
                        road[i].wayP1 = way_point[j];
                        way_point[j].used++;        //路径点被引用
                        x1 = way_point[j].i_xPosition / 100;
                        y1 = way_point[j].i_yPosition / 100;
                        road[i].wayP1 = way_point[j];
                    }
                    if (road[i].i_p2 == way_point[j].i_id)
                    {
                        road[i].wayP2 = way_point[j];
                        way_point[j].used++;
                        x2 = way_point[j].i_xPosition / 100;
                        y2 = way_point[j].i_yPosition / 100;
                        road[i].wayP2 = way_point[j];
                    }
                }
                drawLine(i, road[i].i_id, x1, y1, x2, y2);
            }
            roadNum.Content = count_Road.ToString();
            //自动绘制站点
            for (int i = 0; i < count_Site; i++)
            {
                double x1, y1;
                x1 = site[i].i_xPosition / 100;
                y1 = site[i].i_yPosition / 100;
                drawSite(site[i].i_id, x1, y1);
            }
            siteNum.Content = count_Site.ToString();
            DrawDirect();
        }
        #endregion
        #endregion
        #region 按钮事件
        private void btnOpenMap_Click(object sender, RoutedEventArgs e)
        {
            Thread openMthread = new Thread(new ThreadStart(OpenMap));
            openMthread.Start();
            //OpenMap();
        }
        private void btnOpenXml_Click(object sender, RoutedEventArgs e)
        {
            OpenXml();
        }
        private void btnDrawPo_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = true;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
            deleteElement = false;
        }
        private void btnDrawLine_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            isDrawLine = true;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
        }
        private void btnDrawArc_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            isDrawLine = false;
            isDrawArc = true;
            isDrawSite = false;
            deleteElement = false;
            isLink = false;
        }
        private void btnDrawSite_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            isDrawSite = true;
            isDrawLine = false;
            isDrawArc = false;
            deleteElement = false;
            isLink = false;
        }
        private void btnLink_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            isLink = true;
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
        }
        private void btnModel_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            deleteElement = false;
            isLink = false;
            if (model == 1) //view模式
            {
                ModelView();
            }
            else if (model == 0)
            {
                ModelEdit();
            }
        }
        private void btnPoProof_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
            pTxtid_TextChanged();
            pointProof();
            isFileSaved = false;
        }
        private void btnRoadProof_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
            rTxtThrough_TextChanged();
            rTxtid_TextChanged();
            roadProof();
            isFileSaved = false;
        }
        private void btnSiteProof_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
            sTxtHold_TextChanged();
            sTxtid_TextChanged();
            siteProof();
            isFileSaved = false;
        }
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            deleteElement = true;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
        }
        private void btnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (canvas.Children.Contains(showEle))
            {
                canvas.Children.Remove(showEle);
            }
            isDrawPoint = false;
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
        }
        private void btnPInfo_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
            ShowPInfo();
        }
        private void buSInfo_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
            ShowSInfo();
        }
        private void butRInfo_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
            ShowRInfo();
        }
        private void btnExPort_Click(object sender, RoutedEventArgs e)
        {
            isDrawPoint = false;
            SaveXml();
            deleteElement = false;
            isDrawLine = false;
            isDrawArc = false;
            isDrawSite = false;
            isLink = false;
        }
        #endregion
        #region 更新数据
        private void pointProof()
        {
            canvas.Children.Clear();    //清理画布
            int id = int.Parse(pTxtId.Text.ToString());
            int x = int.Parse(pTxtX.Text.ToString());
            int y = int.Parse(pTxtY.Text.ToString());
            //获取更改的坐标
            for (int i = 0; i < count_Waypoint; i++)
            {
                //路径点坐标
                if (old_poId == way_point[i].i_id)
                {
                    way_point[i].i_xPosition = x;
                    way_point[i].i_yPosition = y;
                    way_point[i].i_id = id;
                }
                //路径
                for (int j = 0; j < count_Road; j++)
                {
                    if (road[j].i_p1 == old_poId)
                    {
                        road[j].i_p1 = id;
                        road[j].wayP1.i_xPosition = x;
                        road[j].wayP1.i_yPosition = y;
                    }
                    else if (road[j].i_p2 == old_poId)
                    {
                        road[j].i_p2 = id;
                        road[j].wayP2.i_xPosition = x;
                        road[j].wayP2.i_yPosition = y;
                    }
                }
            }
            //重绘
            reDraw();
        }
        private void roadProof()
        {
            canvas.Children.Clear();
            int id = int.Parse(rTxtId.Text.ToString());
            int x1 = int.Parse(rTxtStartX.Text.ToString());
            int y1 = int.Parse(rTxtStartY.Text.ToString());
            int x2 = int.Parse(rTxtEndX.Text.ToString());
            int y2 = int.Parse(rTxtEndY.Text.ToString());
            int through = int.Parse(rTxtThrough.Text.ToString());
            int startId = int.Parse(rTxtStartId.Text.ToString());
            int endId = int.Parse(rTxtEndId.Text.ToString());
            //获取更改坐标
            for (int i = 0; i < count_Road; i++)
            {
                if (old_roId == road[i].i_id)
                {
                    road[i].i_id = id;
                    road[i].wayP1.i_xPosition = x1;
                    road[i].wayP1.i_yPosition = y1;
                    road[i].wayP2.i_xPosition = x2;
                    road[i].wayP2.i_yPosition = y2;
                    road[i].i_through = through;
                    road[i].i_p1 = startId;
                    road[i].i_p2 = endId;
                }
                //同时更新路径点的坐标
                for (int j = 0; j < count_Waypoint; j++)
                {
                    if (way_point[j].i_id == startId)
                    {
                        way_point[j].i_id = startId;
                        way_point[j].i_xPosition = x1;
                        way_point[j].i_yPosition = y1;
                    }
                    if (way_point[j].i_id == endId)
                    {
                        way_point[j].i_id = endId;
                        way_point[j].i_xPosition = x2;
                        way_point[j].i_yPosition = y2;
                    }
                }
            }
            //重绘
            reDraw();
        }
        private void siteProof()
        {
            canvas.Children.Clear();
            int id = int.Parse(sTxtId.Text.ToString());
            int x = int.Parse(sTxtX.Text.ToString());
            int y = int.Parse(sTxtY.Text.ToString());
            int or = int.Parse(sTxtOr.Text.ToString());
            int rid = int.Parse(sTxtRoadId.Text.ToString());
            int hold = int.Parse(sTxtHold.Text.ToString());
            //获取更改坐标
            for (int i = 0; i < count_Site; i++)
            {
                if (old_siId == site[i].i_id)
                {
                    site[i].i_id = id;
                    site[i].i_xPosition = x;
                    site[i].i_yPosition = y;
                    site[i].d_or = or;
                    site[i].i_rid = rid;
                    site[i].hold = hold;
                }
            }
            //重绘
            reDraw();
        }
        #endregion
        #region id重复提示/数据合法性检测
        private void pTxtid_TextChanged()
        {
            int tempId = int.Parse(pTxtId.Text.ToLower());
            for (int i = 0; i < count_Waypoint; i++)
            {
                if (way_point[i].delete == 0 && tempId == way_point[i].i_id && tempId != old_poId)
                {
                    MessageBox.Show("该id已被使用，请重新输入");
                    pTxtId.Text = old_poId.ToString();
                    break;
                }
            }
        }
        private void rTxtid_TextChanged()
        {
            int tempId = int.Parse(rTxtId.Text.ToString());
            for (int i = 0; i < count_Road; i++)
            {
                if (road[i].delete == 0 && tempId == road[i].i_id && tempId != old_roId)
                {
                    MessageBox.Show("该id已被使用，请重新输入");
                    rTxtId.Text = old_roId.ToString();
                    break;
                }
            }
        }
        private void rTxtThrough_TextChanged()
        {
            int tempThrough = int.Parse(rTxtThrough.Text.ToString());
            if (tempThrough < 0 || tempThrough > 3)
            {
                MessageBox.Show("‘Through’数据不合法，请重新输入");
                rTxtThrough.Text = old_through.ToString();
            }
        }
        private void sTxtid_TextChanged()
        {
            int temid = int.Parse(sTxtId.Text.ToString());
            for (int i = 0; i < count_Site; i++)
            {
                if (site[i].delete == 0 && temid == site[i].i_id && temid != old_siId)
                {
                    MessageBox.Show("该id已被使用，请重新输入");
                    sTxtId.Text = old_siId.ToString();
                    break;
                }
            }
        }
        private void sTxtHold_TextChanged()
        {
            int tempHold = int.Parse(sTxtHold.Text.ToString());
            if (tempHold != 1 || tempHold != 0)
            {
                MessageBox.Show("‘Hold’数据不合法，请重新输入");
                sTxtHold.Text = old_hold.ToString();
            }
        }
        #endregion
        #region 鼠标吸附
        private void MouseCatch(Point mousePoint)
        {
            double d_X, d_Y, d_Dis;       //坐标差值
            double Min_X = 100, Min_Y = 100, Min_Dis = 10000.0;    //距离最小值
            double d_MouseX, d_MouseY;      //鼠标坐标
            d_MouseX = mousePoint.X;
            d_MouseY = mousePoint.Y;
            //遍历路径点，获取最小值
            for (int i = 0; i < count_Waypoint; i++)
            {
                if (way_point[i].delete == 0)
                {
                    d_X = Math.Abs(d_MouseX - way_point[i].i_xPosition / 100);
                    d_Y = Math.Abs(d_MouseY - height + way_point[i].i_yPosition / 100);
                    d_Dis = Math.Sqrt(Math.Pow(d_X, 2) + Math.Pow(d_Y, 2));
                    if (d_Dis < Min_Dis)
                    {
                        Min_Dis = d_Dis;
                        Min_X = way_point[i].i_xPosition / 100;    //始终保存最小值
                        Min_Y = way_point[i].i_yPosition / 100;
                    }
                }
            }
            //遍历站点，获取最小值
            for (int i = 0; i < count_Site; i++)
            {
                if (site[i].delete == 0)
                {
                    d_X = Math.Abs(d_MouseX - site[i].i_xPosition / 100);
                    d_Y = Math.Abs(d_MouseY - height + site[i].i_yPosition / 100);
                    d_Dis = Math.Sqrt(Math.Pow(d_X, 2) + Math.Pow(d_Y, 2));
                    if (d_Dis < Min_Dis)
                    {
                        Min_Dis = d_Dis;
                        Min_X = site[i].i_xPosition / 100;
                        Min_Y = site[i].i_yPosition / 100;
                    }
                }
            }
            //遍历所有路径，获取最小值
            for (int i = 0; i < count_Road; i++)
            {
                if (road[i].delete == 0)
                {
                    double DirectX, DirectY;    //中点坐标(方向图标坐标)
                    int through = road[i].i_through;
                    DirectX = (road[i].wayP1.i_xPosition + road[i].wayP2.i_xPosition) / 200;
                    DirectY = (road[i].wayP1.i_yPosition + road[i].wayP2.i_yPosition) / 200;
                    d_X = Math.Abs(d_MouseX - DirectX);
                    d_Y = Math.Abs(d_MouseY - height + DirectY);
                    d_Dis = Math.Sqrt(Math.Pow(d_X, 2) + Math.Pow(d_Y, 2));
                    if (d_Dis < Min_Dis)
                    {
                        Min_Dis = d_Dis;
                        Min_X = DirectX;
                        Min_Y = DirectY;
                    }
                }
            }
            Point p = canvas.PointToScreen(new Point(Min_X, height - Min_Y));    //画布相对屏幕坐标
            if (Min_Dis < 3.0)
            {
                double x = p.X, y = p.Y;
                System.Drawing.Point point = new System.Drawing.Point((int)(x), (int)(y));
                System.Windows.Forms.Cursor.Position = point;
            }
        }
        #endregion
        #region 导出XML文件
        public void SaveXml()
        {
            RedundancyCheck();
            XmlNode root = xmlDoc.SelectSingleNode("/map/model");
            XmlNode Pnode = xmlDoc.SelectSingleNode("/map/model/waypoints");
            XmlNode Rnode = xmlDoc.SelectSingleNode("/map/model/roads");
            XmlNode Snode = xmlDoc.SelectSingleNode("/map/model/sites");
            root.RemoveChild(Pnode);    //删除指定节点数据
            root.RemoveChild(Rnode);
            root.RemoveChild(Snode);
            XmlNode node1 = xmlDoc.SelectSingleNode("/map/model");
            XmlNode pointNode = xmlDoc.CreateElement("waypoints");
            XmlElement pointEle;        //路径点结点
            //将路径点数据写入xml文件
            for (int i = 0; i < count_Waypoint; i++)
            {
                if (way_point[i].delete == 0 && way_point[i].used > 0)
                {
                    pointEle = xmlDoc.CreateElement("waypoint");
                    pointEle.SetAttribute("id", way_point[i].i_id.ToString());
                    pointEle.SetAttribute("x", (way_point[i].i_xPosition - originX * 100).ToString());
                    pointEle.SetAttribute("y", (way_point[i].i_yPosition - originY * 100).ToString());
                    pointNode.InnerXml = pointNode.InnerXml.Replace(pointEle.OuterXml,
                        "\n  " + pointEle.OuterXml + "\n");
                    pointNode.AppendChild(pointEle);    //将四级节点数据写入三级节点
                }
            }
            node1.AppendChild(pointNode);
            //路径数据写入
            XmlNode roadNode = xmlDoc.CreateElement("roads");
            XmlElement roadEle;//创建
            for (int i = 0; i < count_Road; i++)
            {
                if (road[i].delete == 0)
                {
                    roadEle = xmlDoc.CreateElement("road");
                    roadEle.SetAttribute("id", road[i].i_id.ToString());
                    roadEle.SetAttribute("p1", road[i].i_p1.ToString());
                    roadEle.SetAttribute("p2", road[i].i_p2.ToString());
                    roadEle.SetAttribute("through", road[i].i_through.ToString());
                    roadNode.InnerXml = roadNode.InnerXml.Replace(roadEle.OuterXml,
                        "\n  " + roadEle.OuterXml + "\n");
                    roadNode.AppendChild(roadEle);
                }
            }
            node1.AppendChild(roadNode);
            //站点数据写入
            XmlNode siteNode = xmlDoc.CreateElement("sites");
            XmlElement siteEle;     //创建
            for (int i = 0; i < count_Site; i++)
            {
                if (site[i].delete == 0)
                {
                    siteEle = xmlDoc.CreateElement("site");
                    siteEle.SetAttribute("id", site[i].i_id.ToString());
                    siteEle.SetAttribute("x", (site[i].i_xPosition - originX * 100).ToString());
                    siteEle.SetAttribute("y", (site[i].i_yPosition - originY * 100).ToString());
                    siteEle.SetAttribute("or", site[i].d_or.ToString());
                    siteEle.SetAttribute("rid", site[i].i_rid.ToString());
                    siteEle.SetAttribute("hold", site[i].hold.ToString());
                    siteNode.InnerXml = siteNode.InnerXml.Replace(siteEle.OuterXml,
                        "\n  " + siteEle.OuterXml + "\n");
                    siteNode.AppendChild(siteEle);
                }
            }
            node1.AppendChild(siteNode);
            Save();
            SortData();
        }
        private void Save()
        {
            SaveFileDialog saveDia = new SaveFileDialog
            {
                Filter = "Xml files(*.xml)|*.xml",
                FilterIndex = 1,
                RestoreDirectory = true
            };
            saveDia.FileName = xmlName;
            if (saveDia.ShowDialog() == true && saveDia.FileName != "")
            {
                xmlDoc.Save(saveDia.FileName.ToString());
                MessageBox.Show(saveDia.FileName.ToString());
                isFileSaved = true;
                ConfigFile();
            }
            else
            {
                MessageBox.Show("Save cancle");
            }
        }
        #endregion
        #region 清空数据及画布
        private void Clear()
        {
            way_point.Clear();
            site.Clear();
            road.Clear();
            count_Waypoint = 0;
            count_Road = 0;
            count_Site = 0;
            count_DrawHand = 0;
            flag_PoDrawHand = 0;
            scaleLe = 1;
            canvas.Children.Clear();
        }
        #endregion
        #region 删除
        private void DeletePoint(waypoints point)
        {
            canvas.Children.Clear();
            //全局遍历需要删除的路径点及相关联的路径
            for (int j = 0; j < count_Road; j++)
            {
                if (road[j].i_p1 == point.i_id
                    || road[j].i_p2 == point.i_id)
                {
                    road[j].delete = 1;
                    for (int k = 0; k < count_Waypoint; k++)
                    {
                        if (way_point[k].delete == 0
                            && (way_point[k].i_id == road[j].i_p1
                            || way_point[k].i_id == road[j].i_p2))
                        {
                            way_point[k].used--;
                        }
                    }
                }
            }
            reDraw();
            Count();
        }
        private void DeleteRoad(roads road)
        {
            canvas.Children.Clear();
            //全局遍历需要删除的路径及相关联路径点
            for (int j = 0; j < count_Waypoint; j++)
            {
                if ((way_point[j].i_id == road.i_p1
                    || way_point[j].i_id == road.i_p2)
                    && way_point[j].used == 0)
                {
                    way_point[j].delete = 1;
                }
                else if (way_point[j].used > 0
                    && (way_point[j].i_id == road.i_p1
                    || way_point[j].i_id == road.i_p2))
                {
                    way_point[j].used--;
                }
            }
            reDraw();
            Count();
        }
        private void Count()
        {
            show_point = 0;
            show_road = 0;
            show_site = 0;
            for (int i = 0; i < count_Waypoint; i++)
            {
                if (way_point[i].delete == 0)
                {
                    show_point++;
                }
            }
            pointNum.Content = show_point.ToString();
            for (int i = 0; i < count_Road; i++)
            {
                if (road[i].delete == 0)
                {
                    show_road++;
                }
            }
            roadNum.Content = show_road.ToString();
            for (int i = 0; i < count_Site; i++)
            {
                if (site[i].delete == 0)
                {
                    show_site++;
                }
            }
            siteNum.Content = show_site.ToString();
        }
        #endregion
        #region 连线
        private void Link()
        {
            int x1 = p1.i_xPosition / 100;
            int y1 = p1.i_yPosition / 100;
            int x2 = p2.i_xPosition / 100;
            int y2 = p2.i_yPosition / 100;
            flag_roId++;
            int id = flag_roId;
            road.Add(new roads()
            {
                i_id = id,
                i_p1 = p1.i_id,
                i_p2 = p2.i_id,
                i_LorA = 1
            });
            road[count_Road].wayP1.i_xPosition = p1.i_xPosition;    //传递路径点数据
            road[count_Road].wayP1.i_yPosition = p1.i_yPosition;
            road[count_Road].wayP1.i_id = p1.i_id;
            road[count_Road].wayP2.i_xPosition = p2.i_xPosition;
            road[count_Road].wayP2.i_yPosition = p2.i_yPosition;
            road[count_Road].wayP2.i_id = p2.i_id;
            drawLine(count_Road, id, x1, y1, x2, y2);
            rTxtId.Text = road[count_Road].i_id.ToString();
            rTxtStartId.Text = road[count_Road].i_p1.ToString();
            rTxtStartX.Text = road[count_Road].wayP1.i_xPosition.ToString();
            rTxtStartY.Text = road[count_Road].wayP1.i_yPosition.ToString();
            rTxtEndId.Text = road[count_Road].i_p2.ToString();
            rTxtEndX.Text = road[count_Road].wayP2.i_xPosition.ToString();
            rTxtEndY.Text = road[count_Road].wayP2.i_yPosition.ToString();
            count_Road++;
            roadNum.Content = count_Road.ToString();
        }
        #endregion
        #region 绘制方向图标
        private void DrawDirect()
        {
            //for (int i = 0; i < count_Road; i++)
            //{
            //    int through = road[i].i_through;
            //    int x1 = road[i].wayP1.i_xPosition / 100;
            //    int y1 = road[i].wayP1.i_yPosition / 100;
            //    int x2 = road[i].wayP2.i_xPosition / 100;
            //    int y2 = road[i].wayP2.i_yPosition / 100;
            //    int x = (x1 + x2) / 2;   //方向标终点坐标
            //    int y = (y1 + y2) / 2;
            //    int a = (int)(0.208 * Math.Abs(x1 - x2));       //计算方向标坐标
            //    int x3, y3, x4, y4;      //方向标起点坐标
            //    #region P1到P2
            //    if (road[i].delete == 0 && through == 1)
            //    {
            //        if (x1 > x2 && y1 == y2)       //p1在p2水平右侧
            //        {
            //            x3 = x - 1;
            //            y3 = y;     //右侧、上方
            //            x4 = x - 5;
            //            y4 = y;     //左侧、下方
            //            drawDirc(x3, y3, x4, y4, 1, "Green");
            //        }
            //        else if (y1 > y2 && x1 == x2)       //p1在p2垂直上方
            //        {
            //            x3 = x;
            //            y3 = y - 1;
            //            x4 = x;
            //            y4 = y - 5;
            //            drawDirc(x3, y3, x4, y4, 1, "Green");
            //        }
            //        else if (x1 < x2 && y1 == y2)       //p1在p2水平左侧
            //        {
            //            x3 = x + 1;
            //            y3 = y;
            //            x4 = x + 5;
            //            y4 = y;
            //            drawDirc(x3, y3, x4, y4, 1, "Green");
            //        }
            //        else if (y1 < y2 && x1 == x2)       //p1在p2垂直下方
            //        {
            //            x3 = x;
            //            y3 = y + 1;
            //            x4 = x;
            //            y4 = y + 5;
            //            drawDirc(x3, y3, x4, y4, 1, "Green");
            //        }
            //    }
            //    #endregion
            //    #region P2到P1
            //    if (road[i].delete == 0 && through == 2)
            //    {
            //        if (x1 < x2 && y1 == y2)        //p1在p2水平左侧
            //        {
            //            x3 = x - 1;
            //            y3 = y;     //右侧、上方
            //            x4 = x - 5;
            //            y4 = y;     //左侧、下方
            //            drawDirc(x3, y3, x4, y4, 2, "Green");
            //        }
            //        else if (y1 < y2 && x1 == x2)        //p1在p2垂直下方
            //        {
            //            x3 = x;
            //            y3 = y + 1;
            //            x4 = x;
            //            y4 = y + 5;
            //            drawDirc(x3, y3, x4, y4, 2, "Green");
            //        }
            //        else if (x1 > x2 && y1 == y2)        //p1在p2水平右侧
            //        {
            //            x3 = x + 1;
            //            y3 = y;
            //            x4 = x + 5;
            //            y4 = y;
            //            drawDirc(x3, y3, x4, y4, 2, "Green");
            //        }
            //        else if (y1 > y2 && x1 == x2)        //p1在p2垂直上方
            //        {
            //            x3 = x;
            //            y3 = y - 1;
            //            x4 = x;
            //            y4 = y - 5;
            //            drawDirc(x3, y3, x4, y4, 2, "Green");
            //        }
            //    }
            //    #endregion
            //}
        }
        public void DrawArrow(Point curPoint, double arrowAngle = Math.PI / 12, double arrowLength = 20)
        {
            // 获取待操作的 Canvas
            Canvas myCanvas = (Canvas)canvas.Children[canvas.Children.Count - 1];
            if (myCanvas == null)
            {
                return;
            }
            // 修改多边形
            Polygon myPolygon = (Polygon)myCanvas.Children[0];
            double x1 = myPolygon.Points[0].X;
            double y1 = myPolygon.Points[0].Y;
            double x2 = curPoint.X;
            double y2 = curPoint.Y;            
            double angleOri = Math.Atan((y2 - y1) / (x2 - x1));      // 起始点线段夹角
            double angleDown = angleOri - arrowAngle;   // 箭头扩张角度
            double angleUp = angleOri + arrowAngle;     // 箭头扩张角度
            int directionFlag = (x2 > x1) ? -1 : 1;     // 方向标识
            double x3 = x2 + ((directionFlag * arrowLength) * Math.Cos(angleDown));   // 箭头第三个点的坐标
            double y3 = y2 + ((directionFlag * arrowLength) * Math.Sin(angleDown));
            double x4 = x2 + ((directionFlag * arrowLength) * Math.Cos(angleUp));     // 箭头第四个点的坐标
            double y4 = y2 + ((directionFlag * arrowLength) * Math.Sin(angleUp));
            myPolygon.Points[0] = new Point(x1, y1);
            myPolygon.Points[1] = new Point(x2, y2);
            myPolygon.Points[2] = new Point(x3, y3);
            myPolygon.Points[3] = new Point(x4, y4);
            myPolygon.Points[4] = new Point(x2, y2);
        }
        #endregion
        #region 最佳视野
        private void Horizon()
        {
            int scalx, scaly;
            Point center;
            //获取视野边界
            for (int i = 0; i < count_Waypoint; i++)
            {
                if (way_point[i].i_xPosition / 100 > fenceX)
                {
                    fenceX = way_point[i].i_xPosition / 100;
                }
                if (way_point[i].i_yPosition / 100 > fenceY)
                {
                    fenceY = way_point[i].i_yPosition / 100;
                }
            }
            for (int i = 0; i < count_Site; i++)
            {
                if (site[i].i_xPosition / 100 > fenceX)
                {
                    fenceX = site[i].i_xPosition / 100;
                }
                if (site[i].i_yPosition / 100 > fenceY)
                {
                    fenceY = site[i].i_yPosition / 100;
                }
            }
            scalx = (int)(width / fenceX);
            scaly = (int)(height / fenceY);
            scaleLe = scalx > scaly ? scaly : scalx;    //获取最小缩放系数
            totalCanvasScale.ScaleX = scaleLe;
            totalCanvasScale.ScaleY = scaleLe;
            center = new Point(0, height);
            AdjustGraph(canvas, center, scaleLe);
            labSca.Content = Math.Round(scaleLe).ToString();
        }
        #endregion
        #region 模式切换
        public void ModelView()
        {
            toolGrid.Visibility = Visibility.Collapsed;
            propertyGrid.Visibility = Visibility.Collapsed;
            canvasGrid.Height = parentDock.Height - 60;     //设置画布外层控件规格
            canvasGrid.Width = parentDock.Width;
            canvas.Height = canvasGrid.Height - 10;         //设置画布规格
            canvas.Width = canvasGrid.Width - 4;
            baseGrid.Width = canvasGrid.Width - 4;
            canvas.Margin = new Thickness(0, 2, 0, 0);
            baseGrid.Margin = new Thickness(0, 44, 0, 0);
            model = 0;
            btnModel.Content = "Edit";
            height = canvas.Height;
            width = canvas.Width;
            if (count_Waypoint != 0 || count_Road != 0 || count_Site != 0)
            {
                canvas.Children.Clear();
                reDraw();
                scaleLe = 1.0;
                labSca.Content = Math.Round(scaleLe, 5).ToString();
                AdjustGraph(canvas, new Point(originX, originY), scaleLe);  //缩放视野
            }
        }
        public void ModelEdit()
        {
            toolGrid.Visibility = Visibility.Visible;
            propertyGrid.Visibility = Visibility.Visible;
            canvasGrid.Height = parentDock.Height - 100;     //设置画布外层控件规格
            canvasGrid.Width = parentDock.Width - 260;
            canvas.Height = canvasGrid.Height - 14;
            canvas.Width = canvasGrid.Width - 16;
            baseGrid.Width = canvasGrid.Width - 4;
            canvas.Margin = new Thickness(0, 0, 0, 0);
            baseGrid.Margin = new Thickness(0, 0, 0, 0);
            model = 1;
            btnModel.Content = "View";
            height = canvas.Height;
            width = canvas.Width;
            if (count_Waypoint != 0 || count_Road != 0 || count_Site != 0)
            {
                canvas.Children.Clear();
                reDraw();
                Horizon();      //调整至最优视角
            }
        }
        #endregion
        #region AGV位置更新
        public void UpdateAGV(int id, int x, int y, int or)
        {
            agvque.Enqueue(new agvs { i_id = id, i_xPosition = x, i_yPosition = y, or = or });
        }
        private void AgvLocation(int id, int x, int y, int or)
        {
            if (agvdic.ContainsKey(id))
            {
                canvas.Children.Remove(agvele[id]);
                agvdic[id].i_xPosition = x;
                agvdic[id].i_yPosition = y;
                drawAGV(id, x / 100, y / 100,
                    agvele[id].Height, agvele[id].Width, txtagvid[id].Height, txtagvid[id].Width);
            }
            else
            {
                txtagvid.Add(id, new TextBox
                {
                    Text = (id % 1000).ToString(),
                    BorderBrush = null,
                    FontSize = 8,
                    Foreground = Brushes.Black,
                    Background = null,
                    IsReadOnly = true,
                    Height = 16,
                    Width = 16,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                });
                agvdic.Add(id, new agvs
                {
                    i_xPosition = x,
                    i_yPosition = y
                });
                agvele.Add(id, new Ellipse()
                {
                    Height = 12.0,
                    Width = 12.0,
                    StrokeThickness = 6
                });
                drawAGV(id, x / 100, y / 100,
                    agvele[id].Height, agvele[id].Width, txtagvid[id].Height, txtagvid[id].Width);
            }
        }
        #endregion
        #region 地图元素偏移
        private void EleMove(int movX, int movY)
        {
            canvas.Children.Clear();
            if (!canvas.Children.Contains(labPos))
            {
                canvas.Children.Add(baseGrid);
            }
            for (int i = 0; i < count_Waypoint; i++)
            {
                int x, y;
                int id;
                if (way_point[i].delete == 0 && way_point[i].used > 0)
                {
                    way_point[i].i_xPosition += movX * 100;     //偏移后坐标
                    way_point[i].i_yPosition += movY * 100;
                    x = way_point[i].i_xPosition / 100;
                    y = way_point[i].i_yPosition / 100;
                    id = way_point[i].i_id;
                    drawPoint(id, x, y);
                }
            }
            for (int i = 0; i < count_Road; i++)
            {
                int x1, y1, x2, y2;
                int id;
                if (road[i].delete == 0)
                {
                    x1 = road[i].wayP1.i_xPosition / 100;
                    y1 = road[i].wayP1.i_yPosition / 100;
                    x2 = road[i].wayP2.i_xPosition / 100;
                    y2 = road[i].wayP2.i_yPosition / 100;
                    id = road[i].i_id;
                    if (road[i].i_LorA == 1)
                    {
                        drawLine(i, id, x1, y1, x2, y2);
                    }
                    else if (road[i].i_LorA == 2)
                    {
                        PathFigure myPathFigure = new PathFigure
                        {
                            StartPoint = new Point(x1, height - y1)
                        };
                        //计算弧线的半轴长度
                        int i_sizeX = (int)Math.Abs(x2 - x1);
                        int i_sizeY = (int)Math.Abs(y2 - y1);
                        int i_size = (int)(Math.Sqrt(Math.Pow(i_sizeX, 2) + Math.Pow(i_sizeY, 2)) * 0.707);
                        try
                        {
                            myPathFigure.Segments.Add(
                                            new ArcSegment(
                                            new Point(x2, height - y2),
                                            new Size(i_size, i_size),
                                            0,                          //椭圆X轴旋转
                                            false, /* IsLargeArc */     //圆弧是否大于180°
                                            SweepDirection.Clockwise,   //正角方向：Clockwise
                                            true /* IsStroked */ ));
                            /// 创建一个PathGeometry以包含该图
                            PathGeometry myPathGeometry = new PathGeometry();
                            myPathGeometry.Figures.Add(myPathFigure);
                            string col = combRoadCol.Text;
                            Color color = (Color)ColorConverter.ConvertFromString(col);
                            // 显示PathGeometry
                            Path myPath = new Path
                            {
                                Stroke = new SolidColorBrush(color),
                                StrokeThickness = 4,
                                Data = myPathGeometry,
                                Tag = road[i].i_id         //路径id
                            };
                            if (road[i].i_through == 0)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 0}";
                            }
                            else if (road[i].i_through == 1)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 1 + "\n" + "Startid:" + road[i].i_p1 + "\n" + "Endid:" + road[i].i_p2}";
                            }
                            else if (road[i].i_through == 2)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 2 + "\n" + "Startid:" + road[i].i_p2 + "\n" + "Endid:" + road[i].i_p1}";
                            }
                            else if (road[i].i_through == 3)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 3}";
                            }
                            Panel.SetZIndex(myPath, 2);
                            canvas.Children.Add(myPath);
                            myPath.MouseLeftButtonDown += Road_MouseLeftButtonDown;
                        }
                        catch (Exception)
                        {

                        }
                    }
                    else if (road[i].i_LorA == 3)
                    {
                        PathFigure myPathFigure = new PathFigure
                        {
                            StartPoint = new Point(x1, height - y1)
                        };
                        //计算弧线的半轴长度
                        int i_sizeX = Math.Abs(x2 - x1);
                        int i_sizeY = Math.Abs(y2 - y1);
                        int i_size = (int)(Math.Sqrt(Math.Pow(i_sizeX, 2) + Math.Pow(i_sizeY, 2)) * 0.707);
                        try
                        {
                            myPathFigure.Segments.Add(
                                            new ArcSegment(
                                            new Point(x2, height - y2),
                                            new Size(i_size, i_size),
                                            0,                          //椭圆X轴旋转
                                            false, /* IsLargeArc */     //圆弧是否大于180°
                                            SweepDirection.Counterclockwise,   //负角方向：Counterclockwise
                                            true /* IsStroked */ ));
                            /// 创建一个PathGeometry以包含该图
                            PathGeometry myPathGeometry = new PathGeometry();
                            myPathGeometry.Figures.Add(myPathFigure);
                            string col = combRoadCol.Text;
                            Color color = (Color)ColorConverter.ConvertFromString(col);
                            // 显示PathGeometry
                            Path myPath = new Path
                            {
                                Stroke = new SolidColorBrush(color),
                                StrokeThickness = 4,
                                Data = myPathGeometry,
                                Tag = road[i].i_id         //路径id
                            };
                            if (road[i].i_through == 0)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 0}";
                            }
                            else if (road[i].i_through == 1)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 1 + "\n" + "Startid:" + road[i].i_p1 + "\n" + "Endid:" + road[i].i_p2}";
                            }
                            else if (road[i].i_through == 2)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 2 + "\n" + "Startid:" + road[i].i_p2 + "\n" + "Endid:" + road[i].i_p1}";
                            }
                            else if (road[i].i_through == 3)
                            {
                                myPath.ToolTip = $"{"id:" + id + "\n" + "Through:" + 3}";
                            }
                            Panel.SetZIndex(myPath, 2);
                            canvas.Children.Add(myPath);
                            myPath.MouseLeftButtonDown += Road_MouseLeftButtonDown;
                        }
                        catch (Exception)
                        {

                        }
                    }
                }
            }
            for (int i = 0; i < count_Site; i++)
            {
                int x, y;
                int id;
                if (site[i].delete == 0)
                {
                    site[i].i_xPosition += movX * 100;
                    site[i].i_yPosition += movY * 100;
                    x = site[i].i_xPosition / 100;
                    y = site[i].i_yPosition / 100;
                    id = site[i].i_id;
                    drawSite(id, x, y);
                }
            }
            DrawDirect();
        }
        #endregion
        #region 元素id信息
        private void ShowPInfo()
        {
            for (int i = 0; i < count_Waypoint; i++)        //路径点id
            {
                if (txtPinfo.ContainsKey(way_point[i].i_id))
                {
                    TextBox txtInfo = txtPinfo[way_point[i].i_id];
                    double x = way_point[i].i_xPosition / 100 - txtInfo.Width / 2;
                    double y = height - way_point[i].i_yPosition / 100 - txtInfo.Height / 2;
                    txtInfo.Margin = new Thickness(x, y, 0, 0);
                    if (txtInfo.Visibility == Visibility.Visible)
                    {
                        txtInfo.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (canvas.Children.Contains(txtInfo))
                        {
                            txtInfo.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            canvas.Children.Add(txtInfo);
                            txtInfo.Visibility = Visibility.Visible;
                        }
                    }
                }
                else
                {
                    TextBox txtInfo = new TextBox
                    {
                        Text = (way_point[i].i_id).ToString(),
                        FontSize = 4,
                        Height = 12,
                        Width = 12,
                        Background = null,
                        IsReadOnly = true,
                        BorderBrush = null,
                        Foreground = Brushes.Black,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    txtPinfo.Add(way_point[i].i_id, txtInfo);
                    double x = way_point[i].i_xPosition / 100 - txtInfo.Width / 2;
                    double y = height - way_point[i].i_yPosition / 100 - txtInfo.Height / 2;
                    txtInfo.Margin = new Thickness(x, y, 0, 0);
                    Panel.SetZIndex(txtInfo, 21);
                    canvas.Children.Add(txtInfo);
                }
            }
        }
        private void ShowRInfo()
        {
            for (int i = 0; i < count_Road; i++)        //路径id
            {
                if (txtRinfo.ContainsKey(road[i].i_id))
                {
                    int Cenx, Ceny;
                    Cenx = (road[i].wayP1.i_xPosition + road[i].wayP2.i_xPosition) / 200;
                    Ceny = (road[i].wayP1.i_yPosition + road[i].wayP2.i_yPosition) / 200;
                    TextBox txtInfo = txtRinfo[road[i].i_id];
                    double x = Cenx - txtInfo.Width / 2;
                    double y = height - Ceny - txtInfo.Height / 2;
                    txtInfo.Margin = new Thickness(x, y, 0, 0);
                    if (txtInfo.Visibility == Visibility.Visible)
                    {
                        txtInfo.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (canvas.Children.Contains(txtInfo))
                        {
                            txtInfo.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            canvas.Children.Add(txtInfo);
                            txtInfo.Visibility = Visibility.Visible;
                        }
                    }
                }
                else
                {
                    TextBox txtInfo = new TextBox
                    {
                        Text = (road[i].i_id).ToString(),
                        FontSize = 4,
                        Height = 12,
                        Width = 12,
                        Background = null,
                        IsReadOnly = true,
                        BorderBrush = null,
                        Foreground = Brushes.Black,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    txtRinfo.Add(road[i].i_id, txtInfo);
                    int Cenx, Ceny;
                    Cenx = (road[i].wayP1.i_xPosition + road[i].wayP2.i_xPosition) / 200;
                    Ceny = (road[i].wayP1.i_yPosition + road[i].wayP2.i_yPosition) / 200;
                    double x = Cenx - txtInfo.Width / 2;
                    double y = height - Ceny - txtInfo.Height / 2;
                    txtInfo.Margin = new Thickness(x, y, 0, 0);
                    Panel.SetZIndex(txtInfo, 21);
                    canvas.Children.Add(txtInfo);
                }
            }
        }
        private void ShowSInfo()
        {
            for (int i = 0; i < count_Site; i++)        //站点id
            {
                if (txtSinfo.ContainsKey(site[i].i_id))
                {
                    TextBox txtInfo = txtSinfo[site[i].i_id];
                    double x = site[i].i_xPosition / 100 - txtInfo.Width / 2;
                    double y = height - site[i].i_yPosition / 100 - txtInfo.Height / 2;
                    txtInfo.Margin = new Thickness(x, y, 0, 0);
                    if (txtInfo.Visibility == Visibility.Visible)
                    {
                        txtInfo.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (canvas.Children.Contains(txtInfo))
                        {
                            txtInfo.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            canvas.Children.Add(txtInfo);
                            txtInfo.Visibility = Visibility.Visible;
                        }
                    }
                }
                else
                {
                    TextBox txtInfo = new TextBox
                    {
                        Text = (site[i].i_id).ToString(),
                        FontSize = 3,
                        Height = 12,
                        Width = 14,
                        Background = null,
                        IsReadOnly = true,
                        BorderBrush = null,
                        Foreground = Brushes.Black,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    txtSinfo.Add(site[i].i_id, txtInfo);
                    double x = site[i].i_xPosition / 100 - txtInfo.Width / 2;
                    double y = height - site[i].i_yPosition / 100 - txtInfo.Height / 2;
                    txtInfo.Margin = new Thickness(x, y, 0, 0);
                    Panel.SetZIndex(txtInfo, 21);
                    canvas.Children.Add(txtInfo);
                }
            }
        }
        #endregion
        #region xml数据排序
        private void SortData()
        {
            XPathDocument xmlPathDoc = new XPathDocument(xmlAddress);
            XmlNode node = xmlDoc.SelectSingleNode("/map/model/waypoints");
            XPathNavigator navigator = xmlPathDoc.CreateNavigator();
            string xpath = string.Format("/map/model/waypoints/waypoint");
            XPathExpression exp = navigator.Compile(xpath);
            exp.AddSort("@id", XmlSortOrder.Ascending, XmlCaseOrder.None, "", XmlDataType.Text);
            XPathNodeIterator nodeIterator = navigator.Select(exp);
            node.RemoveAll();
            while (nodeIterator.MoveNext())
            {
                XmlElement xe = xmlDoc.CreateElement("waypoint");
                xe.SetAttribute("id", nodeIterator.Current.GetAttribute("id", ""));
                xe.SetAttribute("x", nodeIterator.Current.GetAttribute("x", ""));
                xe.SetAttribute("y", nodeIterator.Current.GetAttribute("y", ""));
                node.AppendChild(xe);
            }
            node = xmlDoc.SelectSingleNode("/map/model/roads");
            navigator = xmlPathDoc.CreateNavigator();
            xpath = string.Format("/map/model/roads/road");
            exp = navigator.Compile(xpath);
            exp.AddSort("@id", XmlSortOrder.Ascending, XmlCaseOrder.None, "", XmlDataType.Text);
            nodeIterator = navigator.Select(exp);
            node.RemoveAll();
            while (nodeIterator.MoveNext())
            {
                XmlElement xe = xmlDoc.CreateElement("road");
                xe.SetAttribute("id", nodeIterator.Current.GetAttribute("id", ""));
                xe.SetAttribute("p1", nodeIterator.Current.GetAttribute("p1", ""));
                xe.SetAttribute("p2", nodeIterator.Current.GetAttribute("p2", ""));
                xe.SetAttribute("through", nodeIterator.Current.GetAttribute("through", ""));
                node.AppendChild(xe);
            }
            node = xmlDoc.SelectSingleNode("/map/model/sites");
            navigator = xmlPathDoc.CreateNavigator();
            xpath = string.Format("/map/model/sites/site");
            exp = navigator.Compile(xpath);
            exp.AddSort("@id", XmlSortOrder.Ascending, XmlCaseOrder.None, "", XmlDataType.Text);
            nodeIterator = navigator.Select(exp);
            node.RemoveAll();
            while (nodeIterator.MoveNext())
            {
                XmlElement xe = xmlDoc.CreateElement("site");
                xe.SetAttribute("id", nodeIterator.Current.GetAttribute("id", ""));
                xe.SetAttribute("x", nodeIterator.Current.GetAttribute("x", ""));
                xe.SetAttribute("y", nodeIterator.Current.GetAttribute("y", ""));
                xe.SetAttribute("or", nodeIterator.Current.GetAttribute("or", ""));
                xe.SetAttribute("rid", nodeIterator.Current.GetAttribute("rid", ""));
                xe.SetAttribute("hold", nodeIterator.Current.GetAttribute("hold", ""));
                node.AppendChild(xe);
            }
        }
        #endregion
        #region 配置文件
        private void ConfigFile()
        {
            XmlDocument xmlConfig = new XmlDocument();
            XmlNode header = xmlConfig.CreateXmlDeclaration("1.0", "utf-8", null);
            xmlConfig.AppendChild(header);
            XmlNode root = xmlConfig.CreateElement("Config");
            XmlElement scale = xmlConfig.CreateElement("Scale");
            XmlElement model = xmlConfig.CreateElement("Model");
            XmlElement fencex = xmlConfig.CreateElement("Fencex");
            XmlElement fencey = xmlConfig.CreateElement("Fencey");
            XmlElement totalx = xmlConfig.CreateElement("Totalx");
            XmlElement totaly = xmlConfig.CreateElement("Totaly");
            XmlElement addre = xmlConfig.CreateElement("Address");
            scale.SetAttribute("scale", scaleLe.ToString());
            root.AppendChild(scale);
            model.SetAttribute("model", model.ToString());
            root.AppendChild(model);
            fencex.SetAttribute("fenceX", fenceX.ToString());
            root.AppendChild(fencex);
            fencey.SetAttribute("fenceY", fenceY.ToString());
            root.AppendChild(fencey);
            totalx.SetAttribute("totalX", totalX.ToString());
            root.AppendChild(totalx);
            totaly.SetAttribute("totalY", totalY.ToString());
            root.AppendChild(totaly);
            addre.SetAttribute("Address", xmlAddress.ToString());
            root.AppendChild(addre);
            xmlConfig.AppendChild(root);
            xmlConfig.Save(@".\Config.xml");
        }
        #endregion
        #region 冗余元素检测
        private void RedundancyCheck()
        {
            for (int i = 0; i < count_Waypoint; i++)
            {
                for (int j = 0; j < count_Waypoint; j++)
                {
                    if (way_point[i].i_xPosition == way_point[j].i_xPosition
                        && way_point[i].i_yPosition == way_point[j].i_yPosition)    //存在两个相同点
                    {
                        if (way_point[i].used == 0)     //未被引用的路径点
                        {
                            way_point[i].delete = 1;
                            count_Waypoint--;
                        }
                        else if (way_point[j].used == 0)
                        {
                            way_point[j].delete = 1;
                            count_Waypoint--;
                        }
                    }
                }
            }
            for (int i = 0; i < count_Road; i++)
            {
                if (road[i].wayP1.i_xPosition == road[i].wayP2.i_xPosition
                    && road[i].wayP1.i_yPosition == road[i].wayP2.i_yPosition)  //路径长度为“0”
                {
                    for (int j = 0; j < count_Waypoint; j++)
                    {
                        if (road[i].i_p1 == way_point[j].i_id
                            || road[i].i_p2 == way_point[j].i_id)
                        {
                            way_point[j].delete = 1;        //删除路径点
                            count_Waypoint--;
                        }
                        road[i].delete = 1;                 //删除路径
                        count_Road--;
                    }
                }
            }
            Count();
        }
        #endregion
        #region 坐标数据透明
        private void baseGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            labPos.Opacity = 0.2;
            labpoint.Opacity = 0.2;
            pointNum.Opacity = 0.2;
            labroad.Opacity = 0.2;
            roadNum.Opacity = 0.2;
            labsite.Opacity = 0.2;
            siteNum.Opacity = 0.2;
            labScale.Opacity = 0.2;
            labSca.Opacity = 0.2;
        }
        private void baseGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            labPos.Opacity = 1;
            labpoint.Opacity = 1;
            pointNum.Opacity = 1;
            labroad.Opacity = 1;
            roadNum.Opacity = 1;
            labsite.Opacity = 1;
            siteNum.Opacity = 1;
            labScale.Opacity = 1;
            labSca.Opacity = 1;
        }
        #endregion
    }
}
