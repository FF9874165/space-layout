using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eto.Forms;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using Rhino.Geometry.Intersect;
using static System.Net.Mime.MediaTypeNames;
using static Space_Layout.BasketballMatchBuilding;
using static Space_Layout.BuildingGroup;

namespace Space_Layout
{
    //篮球比赛馆，由于此类单体确定性很高（相比通用球类馆），所以集中于此程序类定义场馆的生成，不再拆分多类
    public class BasketballMatchBuilding : SportsBuilding, ITrans
    {
        //通用
        //观众座椅排布类型
        public enum SeatType
        {
            单侧,
            双侧,
            四周,
        }
        //多播控制运动场地、辅助用房移动
        public delegate void Move(Transform transform);

        #region 变量
        //场馆
        public Move moveDelegate;//移动代理
        public BallCourt basketballCourt;//调用篮球场地数据
        public GH_Box groupBoundary;//场馆外轮廓
        public double rotation;//场馆旋转角度
        public Curve baseBoundary;//基底范围线
        public double baseArea;//基底面积
        public Curve ceilingBoundary;//屋面offset后范围线
        public double ceilingArea;//屋面offset后面积

        //场芯
        public GH_Rectangle court;//场芯
        public GH_Rectangle courtBuffer;//场芯+缓冲区
        public GH_Rectangle courtActual;//将部分辅助用房面积匀给场区
        public double courtTotalArea;//带有缓冲区的球场面积

        //坐席
        public SeatType seatType;
        public List<GH_Rectangle> seats = new List<GH_Rectangle>();
        public double seatArea;//坐席面积

        //辅助用房
        public double auxiliaryRequiredArea;//辅助用房任务书面积
        public List<Auxiliary> auxiliary = new List<Auxiliary>();//辅助用房
        public double columnSpan = 8;
        public double auxiliaryHeight = 6;
        public double boxWidth;//球场X轴长度
        public double boxLength;//球场Y轴长度

        public double areaWidth;//球场短边对应的单侧单层面积
        public double areaLength;//球场长边对应的单侧单层面积
        public double areaWidthAndLength;//球场1短+1长对应单层面积
        public double area3Sides;//球场2短+1长对应单层面积
        public double area4Sides;//球场4周单层面积

        //评价
        public int currentLevel = -1;//布局完成后，场馆所在楼层
        public int itemIndex = -1;//布局完成后，场馆在楼层的第几个
        #endregion

        #region 属性
        //获取朝向
        public Orientation Orientation => orientationBox;

        //接口的属性
        int ITrans.CurrentLevel
        {
            get { return currentLevel; }
            set { currentLevel = value; }
        }
        int ITrans.ItemIndex
        {
            get { return itemIndex; }
            set { currentLevel = value; }
        }
        #endregion
        
        //构造函数
        public BasketballMatchBuilding(int spectator, double area, Point3d showOrigin, double reductionRatio)
        {
            //输入外部数据
            #region
            buildingType = BuildingType.篮球比赛馆;
            basketballCourt = new BallCourt(CourtType.篮球比赛场);
            this.area = area;
            this.showOrigin = showOrigin;
            count = 1;
            this.spectator = spectator;
            hasAuxiliary = true;
            height = 24;//考虑多功能，按辅助用房4F算
            StaticObject.basketballMatchBuilding = this;//指向静态管理类
            #endregion

            //创建实体
            //创建场芯
            court = new GH_Rectangle(new Rectangle3d(Plane.WorldXY, basketballCourt.widthPerCourt, basketballCourt.lengthPerCourt));
            court.Transform(Transform.Translation(Point3d.Origin - court.Value.Center));
            //创建场芯+缓冲区
            courtBuffer = new GH_Rectangle(new Rectangle3d(Plane.WorldXY, basketballCourt.widthPerCourt + 2 * basketballCourt.terminalDistance, basketballCourt.lengthPerCourt + 2 * basketballCourt.sidelineDistance));
            courtBuffer.Transform(Transform.Translation(Point3d.Origin - courtBuffer.Value.Center));
            //获取辅助用房划分给运动场地的面积
            if (reductionRatio != 0)
            {
                double areaAfter = courtBuffer.Value.Area + (area - courtBuffer.Value.Area) * reductionRatio;
                if (areaAfter > courtBuffer.Value.Area)
                    ScaleCourt(courtBuffer.Value.Area, areaAfter);
            }
            else
            {
                courtTotalArea = courtBuffer.Value.Area;
                courtActual = courtBuffer;
            }
            areaCourtGroupRequired.Add(courtTotalArea);
            areaCourtGroupActual.Add(courtTotalArea);
            //创建坐席
            GetSeats();
            //创建辅助用房
            CreateAuxiliary();
            //计算场馆真实面积
            GetAreaTotalGroupActual();
            //移动至展示点
            MoveToPoint(showOrigin);
            //获取基底面积
            GetBaseArea();
            //移动代理注册
            moveDelegate += MoveUseTrans;

        }

        //按比例扩大球场区X+Y轴长度
        public void ScaleCourt(double areaBefore, double areaAfter)
        {
            //缩放差值计算
            double ratio = Math.Sqrt(areaAfter / areaBefore);
            courtActual = new GH_Rectangle(new Rectangle3d(Plane.WorldXY, courtBuffer.Value.Width * ratio, courtBuffer.Value.Height * ratio));
            courtActual.Transform(Transform.Translation(Point3d.Origin - courtActual.Value.Center));
            courtTotalArea = courtActual.Value.Area;
            areaCourtGroupRequired.Add(courtTotalArea);
        }

        //生成坐席范围
        public void GetSeats()
        {
            if ((spectator > 0) && (spectator <= 1200))//边线单侧布置坐席
            {
                seatType = SeatType.单侧;
                seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 0.8 * (spectator / 60), courtActual.Value.Height)));//每排60人
                seatArea = seats[0].Value.Area;
                seats[0].Transform(Transform.Translation(new Vector3d(courtActual.Value.Width / 2, -courtActual.Value.Height / 2, 0)));//归位
            }
            else if ((spectator > 1200) && (spectator <= 2400))//边线对侧布置坐席
            {
                seatType = SeatType.双侧;
                seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 0.8 * (spectator / (60 * 2)), courtActual.Value.Height)));//每排60人
                seatArea = seats[0].Value.Area;
                seats[0].Transform(Transform.Translation(new Vector3d(courtActual.Value.Width / 2, -courtActual.Value.Height / 2, 0)));//归位
                seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 0.8 * (spectator / (60 * 2)), courtActual.Value.Height)));//每排60人
                seatArea += seats[0].Value.Area;
                seats[1].Transform(Transform.Translation(new Vector3d(-(courtActual.Value.Width / 2 + seats[1].Value.Width), -courtActual.Value.Height / 2, 0)));//归位
            }
            else if ((spectator > 2400) && (spectator <= 5000))//回字形布置坐席，上限5000人
            {
                seatType = SeatType.四周;
                if ((spectator > 2400) && (spectator <= 2740))
                {
                    seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 43.2, 54.2)));
                }
                else if ((spectator > 2740) && (spectator <= 3120))
                {
                    seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 44.8, 55.8)));
                }
                else if ((spectator > 3120) && (spectator <= 3472))
                {
                    seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 46.4, 57.4)));
                }
                else if ((spectator > 3472) && (spectator <= 3840))
                {
                    seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 48, 59)));
                }
                else if ((spectator > 3840) && (spectator <= 4260))
                {
                    seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 49.6, 60.6)));
                }
                else if ((spectator > 4260) && (spectator <= 4621))
                {
                    seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 51.2, 62.2)));
                }
                else
                {
                    seats.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, 52.8, 63.8)));
                }
                seatArea = seats[0].Value.Area - courtBuffer.Value.Area;
                seats[0].Transform(Transform.Translation(Point3d.Origin - seats[0].Value.Center));
            }

        }

        //生成辅助用房
        public void CreateAuxiliary()
        {
            //获取辅助用房总面积
            auxiliaryRequiredArea = area - courtActual.Value.Area - seatArea;
            //若>0，则生成辅助用房实体
            if (auxiliaryRequiredArea > 0)
            {
                switch (seatType)
                {
                    #region 单侧
                    case SeatType.单侧:
                        //基础数据计算
                        boxWidth = courtActual.Value.Width + seats[0].Value.Width;
                        boxLength = courtActual.Value.Height + seats[0].Value.Height;
                        areaWidth = boxWidth * columnSpan;
                        //创建辅助用房单体
                        #region
                        if (auxiliaryRequiredArea < areaWidth)
                        {
                            auxiliary.Add(new Auxiliary(auxiliaryRequiredArea / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth) && (auxiliaryRequiredArea < areaWidth * 2))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 2) && (auxiliaryRequiredArea < areaWidth * 3))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 2) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 3) && (auxiliaryRequiredArea < areaWidth * 4))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 3) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        if ((auxiliaryRequiredArea > areaWidth * 4) && (auxiliaryRequiredArea < areaWidth * 5))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 4) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 5) && (auxiliaryRequiredArea < areaWidth * 6))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 5) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 6) && (auxiliaryRequiredArea < areaWidth * 7))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 6) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 7) && (auxiliaryRequiredArea < areaWidth * 8))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 7) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        #endregion

                        //移动至指定位置
                        #region
                        Point3d[] courtCorner = courtActual.Value.BoundingBox.GetCorners();
                        Point3d[] auxiliaryCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
                        for (int i = 0; i < auxiliary.Count; i++)
                        {
                            if (i % 2 == 0)
                            {
                                Transform move = Transform.Translation(courtCorner[0] - auxiliaryCorner[3] + Vector3d.ZAxis * 6 * (i / 2));
                                auxiliary[i].auxiliaryUnit.Transform(move);
                            }
                            else
                            {
                                Transform move = Transform.Translation(courtCorner[3] - auxiliaryCorner[0] + Vector3d.ZAxis * 6 * (i / 2));
                                auxiliary[i].auxiliaryUnit.Transform(move);
                            }
                        }
                        #endregion

                        //创建建筑单体轮廓
                        #region
                        if (auxiliary.Count < 1)
                        {
                            groupBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, courtActual.Value.Width + seats[0].Value.Width), new Interval(0, courtActual.Value.Height + columnSpan), new Interval(0, height)));
                        }
                        else
                        {
                            groupBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, courtActual.Value.Width + seats[0].Value.Width), new Interval(0, courtActual.Value.Height + columnSpan * 2), new Interval(0, height)));
                        }
                        auxiliaryCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
                        groupBoundary.Transform(Transform.Translation(auxiliaryCorner[0] - Point3d.Origin));
                        #endregion
                        break;
                    #endregion

                    #region 双侧
                    case SeatType.双侧:
                        //基础数据计算
                        boxWidth = courtActual.Value.Width + seats[0].Value.Width * 2;
                        boxLength = courtActual.Value.Height + seats[0].Value.Height * 2;
                        areaWidth = boxWidth * columnSpan;
                        //创建辅助用房单体
                        #region
                        if (auxiliaryRequiredArea < areaWidth)
                        {
                            auxiliary.Add(new Auxiliary(auxiliaryRequiredArea / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth) && (auxiliaryRequiredArea < areaWidth * 2))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 2) && (auxiliaryRequiredArea < areaWidth * 3))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 2) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 3) && (auxiliaryRequiredArea < areaWidth * 4))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 3) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 4) && (auxiliaryRequiredArea < areaWidth * 5))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 4) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 5) && (auxiliaryRequiredArea < areaWidth * 6))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 5) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 6) && (auxiliaryRequiredArea < areaWidth * 7))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 6) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 7) && (auxiliaryRequiredArea < areaWidth * 8))
                        {
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
                            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 7) / columnSpan, columnSpan, auxiliaryHeight, true));
                        }
                        #endregion

                        //移动至指定位置
                        #region
                        courtCorner = seats[1].Value.BoundingBox.GetCorners();
                        auxiliaryCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
                        for (int i = 0; i < auxiliary.Count; i++)
                        {
                            if (i % 2 == 0)
                            {
                                Transform move = Transform.Translation(courtCorner[0] - auxiliaryCorner[3] + Vector3d.ZAxis * 6 * (i / 2));
                                auxiliary[i].auxiliaryUnit.Transform(move);
                            }
                            else
                            {
                                Transform move = Transform.Translation(courtCorner[3] - auxiliaryCorner[0] + Vector3d.ZAxis * 6 * (i / 2));
                                auxiliary[i].auxiliaryUnit.Transform(move);
                            }
                        }
                        #endregion

                        //创建建筑单体轮廓
                        #region
                        if (auxiliary.Count < 1)
                        {
                            groupBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, courtActual.Value.Width + seats[0].Value.Width * 2), new Interval(0, courtActual.Value.Height + columnSpan), new Interval(0, height)));
                        }
                        else
                        {
                            groupBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, courtActual.Value.Width + seats[0].Value.Width * 2), new Interval(0, courtActual.Value.Height + columnSpan * 2), new Interval(0, height)));
                        }
                        auxiliaryCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
                        groupBoundary.Transform(Transform.Translation(auxiliaryCorner[0] - Point3d.Origin));
                        #endregion
                        break;
                    #endregion

                    #region 四周
                    case SeatType.四周:
                        //基础数据计算
                        boxWidth = seats[0].Value.Width;
                        boxLength = seats[0].Value.Height;
                        areaWidth = boxWidth * columnSpan;
                        areaLength = boxLength * columnSpan;
                        areaWidthAndLength = areaWidth + areaLength + columnSpan * columnSpan;
                        area3Sides = areaWidthAndLength + areaWidth + columnSpan * columnSpan;
                        area4Sides = area3Sides + areaLength + columnSpan * columnSpan * 2;

                        //创建辅助用房单体
                        #region
                        if (auxiliaryRequiredArea < areaWidth)//1短1F
                        {
                            CreateFirstAuxiliary();
                        }
                        else if ((auxiliaryRequiredArea > areaWidth) && (auxiliaryRequiredArea < areaWidth * 2))//1短2F
                        {

                            CreateFixedFirstAuxiliary();//短1F
                            CreateSecondAuxiliary();//短2F
                        }
                        else if ((auxiliaryRequiredArea > areaWidth * 2) && (auxiliaryRequiredArea < areaWidthAndLength + areaWidth))//1短2F+1长1F
                        {
                            CreateFixedFirstAuxiliary();//短1F
                            CreateFixedSecondAuxiliary();//短2F
                            CreateThirdAuxiliary();//长1F
                        }
                        else if ((auxiliaryRequiredArea > areaWidthAndLength + areaWidth) && (auxiliaryRequiredArea < areaWidthAndLength * 2))//1短2F+1长2F
                        {
                            CreateFixedFirstAuxiliary();//短1F
                            CreateFixedSecondAuxiliary();//短2F
                            CreateFixedThirdAuxiliary();//长1F
                            CreateFourthAuxiliary();//长2F
                        }
                        else if ((auxiliaryRequiredArea > areaWidthAndLength * 2) && (auxiliaryRequiredArea < area3Sides + areaWidthAndLength))//1短2F+1长2F+1短1F
                        {
                            CreateFixedFirstAuxiliary();//短1F
                            CreateFixedSecondAuxiliary();//短2F
                            CreateFixedThirdAuxiliary();//长1F
                            CreateFixedFourthAuxiliary();//长2F
                            CreateFifthAuxiliary();//北短1F
                        }
                        else if ((auxiliaryRequiredArea > area3Sides + areaWidthAndLength) && (auxiliaryRequiredArea < area3Sides * 2))//1短2F+1长2F+1短2F
                        {
                            CreateFixedFirstAuxiliary();//短1F
                            CreateFixedSecondAuxiliary();//短2F
                            CreateFixedThirdAuxiliary();//长1F
                            CreateFixedFourthAuxiliary();//长2F
                            CreateFixedFifthAuxiliary();//北短1F
                            CreateSixthAuxiliary();//北短2F
                        }
                        else if ((auxiliaryRequiredArea > area3Sides * 2) && (auxiliaryRequiredArea < area4Sides + area3Sides))//1短2F+1长2F+1短2F+1长1F
                        {
                            CreateFixedFirstAuxiliary();//短1F
                            CreateFixedSecondAuxiliary();//短2F
                            CreateFixedThirdAuxiliary();//长1F
                            CreateFixedFourthAuxiliary();//长2F
                            CreateFixedFifthAuxiliary();//北短1F
                            CreateFixedSixthAuxiliary();//北短2F
                            CreatSeventhAuxiliary();//西长1F
                        }
                        else if ((auxiliaryRequiredArea > area4Sides + area3Sides) && (auxiliaryRequiredArea < area4Sides * 2))//四周2F
                        {
                            CreateFixedFirstAuxiliary();//短1F
                            CreateFixedSecondAuxiliary();//短2F
                            CreateFixedThirdAuxiliary();//长1F
                            CreateFixedFourthAuxiliary();//长2F
                            CreateFixedFifthAuxiliary();//北短1F
                            CreateFixedSixthAuxiliary();//北短2F
                            CreatFixedSeventhAuxiliary();//西长1F
                            CreatEighthAuxiliary();//西长2F
                        }
                        #endregion

                        //创建建筑单体轮廓
                        #region
                        GH_Box courtTemp = new GH_Box(new Box(Plane.WorldXY, new Interval(0, boxWidth), new Interval(0, boxLength), new Interval(0, height)));
                        courtTemp.Transform(Transform.Translation(Point3d.Origin - courtTemp.Value.Center + Vector3d.ZAxis * height * 0.5));
                        BoundingBox temp = courtTemp.Boundingbox;
                        for (int i = 0; i < auxiliary.Count; i++)
                        {
                            temp = BoundingBox.Union(temp, auxiliary[i].auxiliaryUnit.Boundingbox);
                        }
                        groupBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(temp.Min.X, temp.Max.X), new Interval(temp.Min.Y, temp.Max.Y), new Interval(0, height)));
                        groupBoundary.Transform(Transform.Translation(temp.Center - groupBoundary.Value.Center));
                        #endregion
                        break;
                        #endregion
                }
            }
            //计算辅助用房面积
            GetAreaAxiliaryGroupActual();
        }

        //创建四周型坐席的单个辅助用房
        #region 
        public void CreateFirstAuxiliary()//南侧短边1F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary(auxiliaryRequiredArea / columnSpan, columnSpan, auxiliaryHeight, true));
            Point3d[] courtCorner = seats[0].Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[0] - auxiliaryCorner[3]);
            auxiliary[0].auxiliaryUnit.Transform(move);
        }
        public void CreateFixedFirstAuxiliary()//南侧短边1F辅助空间，满长
        {
            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
            Point3d[] courtCorner = seats[0].Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[0] - auxiliaryCorner[3]);
            auxiliary[0].auxiliaryUnit.Transform(move);
        }
        public void CreateSecondAuxiliary()//南侧短边2F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth) / columnSpan, columnSpan, auxiliaryHeight, true));
            Point3d[] courtCorner = seats[0].Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[1].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[0] - auxiliaryCorner[3] + Vector3d.ZAxis * auxiliaryHeight);
            auxiliary[1].auxiliaryUnit.Transform(move);
        }
        public void CreateFixedSecondAuxiliary()//南侧短边2F辅助空间，满长
        {
            auxiliary.Add(new Auxiliary(boxWidth, columnSpan, auxiliaryHeight, true));
            Point3d[] courtCorner = seats[0].Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[1].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[0] - auxiliaryCorner[3] + Vector3d.ZAxis * auxiliaryHeight);
            auxiliary[1].auxiliaryUnit.Transform(move);
        }
        public void CreateThirdAuxiliary()//东侧长边1F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidth * 2) / columnSpan, columnSpan, auxiliaryHeight, true));
            Tool.IFRotateHalfPie(ref auxiliary[2].rotation, 0);
            auxiliary[2].auxiliaryUnit.Transform(Transform.Rotation(auxiliary[2].rotation, auxiliary[2].auxiliaryUnit.Value.Center));
            Point3d[] courtCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[2].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[1] - auxiliaryCorner[0]);
            auxiliary[2].auxiliaryUnit.Transform(move);
        }
        public void CreateFixedThirdAuxiliary()//东侧长边1F辅助空间，满长
        {
            auxiliary.Add(new Auxiliary(boxLength + columnSpan, columnSpan, auxiliaryHeight, true));
            Tool.IFRotateHalfPie(ref auxiliary[2].rotation, 0);
            auxiliary[2].auxiliaryUnit.Transform(Transform.Rotation(auxiliary[2].rotation, auxiliary[2].auxiliaryUnit.Value.Center));
            Point3d[] courtCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[2].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[1] - auxiliaryCorner[0]);
            auxiliary[2].auxiliaryUnit.Transform(move);
        }
        public void CreateFourthAuxiliary()//东侧长边2F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidthAndLength - areaWidth) / columnSpan, columnSpan, auxiliaryHeight, true));
            Tool.IFRotateHalfPie(ref auxiliary[3].rotation, 0);
            auxiliary[3].auxiliaryUnit.Transform(Transform.Rotation(auxiliary[3].rotation, auxiliary[3].auxiliaryUnit.Value.Center));
            Point3d[] courtCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[3].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[1] - auxiliaryCorner[0] + Vector3d.ZAxis * auxiliaryHeight);
            auxiliary[3].auxiliaryUnit.Transform(move);
        }
        public void CreateFixedFourthAuxiliary()//东侧长边2F辅助空间，满长
        {
            auxiliary.Add(new Auxiliary(boxLength + columnSpan, columnSpan, auxiliaryHeight, true));
            Tool.IFRotateHalfPie(ref auxiliary[3].rotation, 0);
            auxiliary[3].auxiliaryUnit.Transform(Transform.Rotation(auxiliary[3].rotation, auxiliary[3].auxiliaryUnit.Value.Center));
            Point3d[] courtCorner = auxiliary[0].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[3].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[1] - auxiliaryCorner[0] + Vector3d.ZAxis * auxiliaryHeight);
            auxiliary[3].auxiliaryUnit.Transform(move);
        }
        public void CreateFifthAuxiliary()//北侧短边1F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidthAndLength * 2) / columnSpan, columnSpan, auxiliaryHeight, true));
            Point3d[] courtCorner = auxiliary[2].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[4].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[2] - auxiliaryCorner[1]);
            auxiliary[4].auxiliaryUnit.Transform(move);
        }
        public void CreateFixedFifthAuxiliary()//北侧短边1F辅助空间，满长
        {
            auxiliary.Add(new Auxiliary(boxWidth + columnSpan, columnSpan, auxiliaryHeight, true));
            Point3d[] courtCorner = auxiliary[2].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[4].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[2] - auxiliaryCorner[1]);
            auxiliary[4].auxiliaryUnit.Transform(move);
        }
        public void CreateSixthAuxiliary()//北侧短边2F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - areaWidthAndLength - area3Sides) / columnSpan, columnSpan, auxiliaryHeight, true));
            Point3d[] courtCorner = auxiliary[2].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[5].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[2] - auxiliaryCorner[1] + Vector3d.ZAxis * auxiliaryHeight);
            auxiliary[5].auxiliaryUnit.Transform(move);
        }
        public void CreateFixedSixthAuxiliary()//北侧短边2F辅助空间，满长
        {
            auxiliary.Add(new Auxiliary(boxWidth + columnSpan, columnSpan, auxiliaryHeight, true));
            Point3d[] courtCorner = auxiliary[2].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[5].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[2] - auxiliaryCorner[1] + Vector3d.ZAxis * auxiliaryHeight);
            auxiliary[5].auxiliaryUnit.Transform(move);
        }
        public void CreatSeventhAuxiliary()//西侧长边1F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - area3Sides * 2) / columnSpan, columnSpan, auxiliaryHeight, true));
            Tool.IFRotateHalfPie(ref auxiliary[6].rotation, 0);
            auxiliary[6].auxiliaryUnit.Transform(Transform.Rotation(auxiliary[6].rotation, auxiliary[6].auxiliaryUnit.Value.Center));
            Point3d[] courtCorner = auxiliary[4].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[6].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[3] - auxiliaryCorner[2]);
            auxiliary[6].auxiliaryUnit.Transform(move);
        }
        public void CreatFixedSeventhAuxiliary()//西侧长边1F辅助空间，满长
        {
            auxiliary.Add(new Auxiliary(boxLength + columnSpan * 2, columnSpan, auxiliaryHeight, true));
            Tool.IFRotateHalfPie(ref auxiliary[6].rotation, 0);
            auxiliary[6].auxiliaryUnit.Transform(Transform.Rotation(auxiliary[6].rotation, auxiliary[6].auxiliaryUnit.Value.Center));
            Point3d[] courtCorner = auxiliary[4].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[6].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[3] - auxiliaryCorner[2]);
            auxiliary[6].auxiliaryUnit.Transform(move);
        }
        public void CreatEighthAuxiliary()//西侧长边2F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary((auxiliaryRequiredArea - area3Sides - area4Sides) / columnSpan, columnSpan, auxiliaryHeight, true));
            Tool.IFRotateHalfPie(ref auxiliary[7].rotation, 0);
            auxiliary[7].auxiliaryUnit.Transform(Transform.Rotation(auxiliary[7].rotation, auxiliary[7].auxiliaryUnit.Value.Center));
            Point3d[] courtCorner = auxiliary[4].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[7].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[3] - auxiliaryCorner[2] + Vector3d.ZAxis * auxiliaryHeight);
            auxiliary[7].auxiliaryUnit.Transform(move);
        }
        public void CreatFixedEighthAuxiliary()//西侧长边2F辅助空间，未满
        {
            auxiliary.Add(new Auxiliary(boxLength + columnSpan * 2, columnSpan, auxiliaryHeight, true));
            Tool.IFRotateHalfPie(ref auxiliary[7].rotation, 0);
            auxiliary[7].auxiliaryUnit.Transform(Transform.Rotation(auxiliary[7].rotation, auxiliary[7].auxiliaryUnit.Value.Center));
            Point3d[] courtCorner = auxiliary[4].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Point3d[] auxiliaryCorner = auxiliary[7].auxiliaryUnit.Value.BoundingBox.GetCorners();
            Transform move = Transform.Translation(courtCorner[3] - auxiliaryCorner[2] + Vector3d.ZAxis * auxiliaryHeight);
            auxiliary[7].auxiliaryUnit.Transform(move);
        }
        #endregion

        //计算辅助用房实际总面积
        public void GetAreaAxiliaryGroupActual()
        {
            double tempArea = 0;
            foreach (var item in auxiliary)
            {
                tempArea += item.auxiliaryUnit.Value.Volume / auxiliaryHeight;
            }
            areaAuxiliaryGroupRequired.Add(auxiliaryRequiredArea);
            areaAuxiliaryGroupActual.Add(tempArea);
        }

        //计算篮球馆实际总面积
        public void GetAreaTotalGroupActual()
        {
            areaTotalGroupRequired.Add(area);
            areaTotalGroupActual.Add(courtActual.Value.Area + seatArea + areaAuxiliaryGroupActual[0]);
        }

        //一定机率旋转建筑
        public void RotateBuilding()
        {
            Tool.IFRotateHalfPie(ref rotation, 0.5);
            MustRotate(rotation);
        }
        //旋转
        public void MustRotate(double angle)
        {
            Transform transform = Transform.Rotation(angle, groupBoundary.Value.Center);
            court.Transform(transform);
            courtBuffer.Transform(transform);
            foreach (var item in seats)
            {
                item.Transform(transform);
            }
            foreach (var item in auxiliary)
            {
                item.auxiliaryUnit.Transform(transform);
            }
            groupBoundary.Transform(transform);
        }
        //数据输入后，面向用户的体量展示，将多组同类场馆拉开距离
        public void MoveToPoint(Point3d toPoint)
        {
            //构建移动向量
            Point3d[] fromPoint = groupBoundary.Value.GetCorners();
            Transform trans;
            if (rotation == 0)
            {
                trans = Transform.Translation(toPoint - fromPoint[0]);

            }
            else
            {
                trans = Transform.Translation(toPoint - fromPoint[3]);
            }
            //逐一移动实体对象
            court.Transform(trans);
            courtBuffer.Transform(trans);
            foreach (var item in seats)
            {
                item.Transform(trans);
            }
            foreach (var item in auxiliary)
            {
                item.auxiliaryUnit.Transform(trans);
            }
            groupBoundary.Transform(trans);
        }
        //场馆整体移动
        public void MoveUseTrans(Transform transform)
        {
            #region 场馆
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            baseCenter.Transform(transform);
            #endregion

            #region 场地及看台
            court.Transform(transform);
            courtBuffer.Transform(transform);
            foreach (var item in seats)
            {
                item.Transform(transform);
            }
            #endregion

            #region 辅助
            foreach (var item in auxiliary)
            {
                item.auxiliaryUnit.Transform(transform);
            }
            #endregion
        }
        //获取基底面积
        public void GetBaseArea()
        {
            #region 获取底面中心点
            baseCenter = new Point3d(groupBoundary.Value.Center.X, groupBoundary.Value.Center.Y, 0);
            #endregion

            #region 获取基底边界
            //获取运动场地顶面
            List<Brep> breps = new List<Brep>();
            Brep courtBrep = groupBoundary.Value.ToBrep();
            BrepFaceList courtBrepList = courtBrep.Faces;
            Brep courtTop = courtBrepList.ExtractFace(5);
            breps.Add(courtTop);
            //获取顶面的边界曲线
            List<Curve> curveAll = new List<Curve>();//临时存放
            for (int i = 0; i < breps.Count; i++)
            {
                Curve[] tempFrame = breps[i].GetWireframe(0);
                Curve[] tempFrame2 = Curve.JoinCurves(tempFrame);
                tempFrame2[0].MakeClosed(0);
                curveAll.Add(tempFrame2[0]);
            }
            #endregion

            #region 实体搭建
            Curve[] baseBoundaries = Curve.CreateBooleanUnion(curveAll);
            baseBoundaries[0].MakeClosed(0);
            baseBoundary = baseBoundaries[0];
            Curve[] ceiling = baseBoundaries[0].Offset(Plane.WorldXY, StaticObject.offset, 1, CurveOffsetCornerStyle.Sharp);
            ceiling[0].MakeClosed(0);
            ceilingBoundary = ceiling[0];
            baseBoundary.Transform(Transform.Translation(0, 0, -24));
            #endregion

            #region 数据更新
            baseArea = courtTop.GetArea();
            AreaMassProperties compute = AreaMassProperties.Compute(ceilingBoundary);
            ceilingArea = compute.Area;
            #endregion
        }
        //平面布局中的移动
        public void LayoutMove()
        {

        }
        //ITrans接口，实现移动至指定点
        public void SetToPoint(Point3d point3d)
        {
            //构建移动向量
            //Point3d[] fromPoint = groupBoundary.Value.GetCorners();
            Transform trans;
            trans = Transform.Translation(point3d - baseCenter);
            //逐一移动实体对象
            moveDelegate(trans);
        }
        //将对象拉入边界内
        public int DragIn(GH_Curve curve)
        {
            int failTime = 0;
            int limitation = 5;
            //baseBoundary.Transform(Transform.Translation(0,0,-24));
            while ((Curve.PlanarCurveCollision(baseBoundary, curve.Value, Plane.WorldXY, 0.1)) && (failTime < limitation))
            {
                Point3d[] vetices = baseBoundary.GetBoundingBox(true).GetCorners();//获取场馆边界点
                List<Vector3d> vectors = new List<Vector3d>();//存储场馆与边线交点
                Vector3d move = Vector3d.Zero;//最终选用的移动变量
                for (int i = 0; i < vetices.Length; i++)
                {
                    if ((SiteInfo.siteRetreat.Value.Contains(vetices[i]) == PointContainment.Outside))
                    {
                        double t;
                        SiteInfo.siteRetreat.Value.ClosestPoint(vetices[i], out t);
                        Point3d p = SiteInfo.siteRetreat.Value.PointAt(t);
                        vectors.Add(new Vector3d(p - vetices[i]));
                    }
                }
                foreach (var v in vectors)
                {
                    if (move.Length < v.Length)
                    {
                        move = v;
                    }
                }
                moveDelegate(Transform.Translation(move));
                failTime += 1;
            }
            if (failTime > limitation) { return 2; }
            else { return 0; }
        }

        //设置朝向
        public void SetOrientation()
        {
            double xSize = groupBoundary.Boundingbox.Max.X - groupBoundary.Boundingbox.Min.X;
            double ySize = groupBoundary.Boundingbox.Max.Y - groupBoundary.Boundingbox.Min.Y;
            if (xSize < ySize)
            {
                orientationBox = Orientation.垂直;
            }
            else
            {
                orientationBox = Orientation.水平;
            }
        }
        //场馆实体整体移动
        public void Trans(Transform transform)
        {
            moveDelegate(transform);
        }
        //获取底面一半X、Y尺寸,0=X,1=Y
        public double GetHalfBaseSize(int orientation, GH_Box groupB)
        {
            double detaX = groupB.Value.BoundingBox.Diagonal.X / 2;
            double detaY = groupB.Value.BoundingBox.Diagonal.Y / 2;
            if (orientation == 0) { return detaX; }
            else { return detaY; }
        }
        //获取外轮廓box
        public GH_Box GetBoundaryBox()
        {
            return groupBoundary;
        }
        //获取中心点
        public Point3d GetCenterPoint()
        {
            return baseCenter;
        }
        //创建写入JSON用的简化场馆对象
        public SimplifiedBuilding CreateSimplifiedBuilding()
        {
            //获取辅助用房
            List<GH_Box> gymAuxiliaries=new List<GH_Box>();
            for (int i = 0; i < auxiliary.Count; i++)
            {
                gymAuxiliaries.Add(auxiliary[i].auxiliaryUnit);
            }
            //构造新的简化场馆
            return new SimplifiedBuilding(buildingType, groupBoundary.Value.Center, groupBoundary, gymAuxiliaries, new List<GH_Rectangle>() { court}) ;
        }
    }
}
