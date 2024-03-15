using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //针对300-800人观演厅
    public class Theater : Building, ITrans
    {
        #region 变量
        //观演厅
        public int spectator;
        public double hallWidth;
        public double hallLength;
        public GH_Box hall;
        public double hallArea;
        //辅助用房
        public List<TheaterUnit> theaterUnits = new List<TheaterUnit>();
        public double columnSpan = 8;//房间进深
        public double auxiliaryHeight = 6;
        //整体场馆
        public GH_Box groupBoundary;
        public Curve baseBoundary;//基底范围线
        public double baseArea;//基底面积
        public Curve ceilingBoundary;//屋面offset后范围线
        public double ceilingArea;//屋面offset后面积

        //获取朝向
        public Orientation Orientation => orientationBox;
        #endregion

        #region 属性
        //接口的属性
        int ITrans.CurrentLevel
        {
            get { return currentLevel; }
            set { currentLevel = value; }
        }
        int ITrans.ItemIndex
        {
            get { return itemIndex; }
            set { itemIndex = value; }
        }
        #endregion

        //构造函数
        public Theater(double area, int spectator, Point3d origin)
        {
            StaticObject.theater = this;
            buildingType = BuildingType.观演厅;
            this.area = area;
            this.spectator = spectator;
            this.showOrigin = origin;
            this.height = 12;

            CreateHall();
            CreateAuxiliary();
            GetBoundary();
            //获取随机旋转90度的弧度
            //double rotation = 0;
            //Tool.IFRotateHalfPie(ref rotation, 0.5);
            //Move(Transform.Rotation(rotation, groupBoundary.Value.Center));
            //将场馆单体移至预设点进行展示
            MoveToShowPoint();
        }
        //创建观演厅
        public void CreateHall()
        {
            if (this.spectator < 500)
            {
                hallWidth = 25;
                hallLength = 15 + 2 + (Math.Ceiling((double)spectator / 28));
            }
            else
            {
                hallWidth = 30;
                hallLength = 15 + 2 + (Math.Ceiling((double)spectator / 37));
            }
            hall = new GH_Box(new Box(Plane.WorldXY, new Interval(0, hallWidth), new Interval(0, hallLength), new Interval(0, height)));
            hallArea = hallWidth * hallLength;
        }
        //创建观演厅(短边X轴)
        public void CreateAuxiliary()
        {
            //后台辅助用房
            theaterUnits.Add(new TheaterUnit(-columnSpan, hallWidth + columnSpan, -columnSpan, 0, auxiliaryHeight));
            theaterUnits.Add(new TheaterUnit(-columnSpan, hallWidth + columnSpan, -columnSpan, 0, auxiliaryHeight));
            //右边跨
            theaterUnits.Add(new TheaterUnit(hallWidth, hallWidth + columnSpan, 0, hallLength, auxiliaryHeight));
            theaterUnits.Add(new TheaterUnit(hallWidth, hallWidth + columnSpan, 0, hallLength, auxiliaryHeight));
            //左边跨
            theaterUnits.Add(new TheaterUnit(-columnSpan, 0, 0, hallLength, auxiliaryHeight));
            theaterUnits.Add(new TheaterUnit(-columnSpan, 0, 0, hallLength, auxiliaryHeight));
            //大厅
            double detaY = (area - hallArea - ((hallWidth + columnSpan * 2) * columnSpan + columnSpan * (columnSpan + hallLength) * 2) * 2) / (hallWidth + columnSpan * 2);
            theaterUnits.Add(new TheaterUnit(-columnSpan, hallWidth + columnSpan, hallLength, hallLength + detaY, auxiliaryHeight));
            theaterUnits.Add(new TheaterUnit(-columnSpan, hallWidth + columnSpan, hallLength, hallLength + detaY, auxiliaryHeight));
            for (int i = 0; i < theaterUnits.Count; i++)
            {
                if (i % 2 != 0)
                {
                    theaterUnits[i].theaterUnit.Transform(Transform.Translation(Vector3d.ZAxis * auxiliaryHeight));
                }
            }
        }
        // 获取场馆边界
        public void GetBoundary()
        {
            //创建场馆单体边界
            #region
            BoundingBox temp = hall.Boundingbox;
            for (int i = 0; i < theaterUnits.Count; i++)
            {
                temp = BoundingBox.Union(temp, theaterUnits[i].theaterUnit.Boundingbox);
            }
            groupBoundary = new GH_Box(temp);
            #endregion

            //更新相关边界、指标
            GetBaseArea();
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
            #endregion

            #region 数据更新
            baseArea = courtTop.GetArea();
            AreaMassProperties compute = AreaMassProperties.Compute(ceilingBoundary);
            ceilingArea = compute.Area;
            #endregion
        }
        //移动场馆单体及内部实体
        public void Move(Transform transform)
        {
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            hall.Transform(transform);
            for (int i = 0; i < theaterUnits.Count; i++)
            {
                theaterUnits[i].theaterUnit.Transform(transform);
            }
            baseCenter.Transform(transform);
        }
        //将场馆单体移至预设点进行展示
        public void MoveToShowPoint()
        {
            Point3d[] points = groupBoundary.Value.GetCorners();
            Transform trans;
            double detaX = groupBoundary.Boundingbox.Max.X - groupBoundary.Boundingbox.Min.X;
            double detaY = groupBoundary.Boundingbox.Max.Y - groupBoundary.Boundingbox.Min.Y;
            if (Math.Abs(detaX) > Math.Abs(detaY))
            {
                trans = Transform.Translation(showOrigin - points[3]);
            }
            else
            {
                trans = Transform.Translation(showOrigin - points[0]);
            }
            Move(trans);
        }
        //平面布局中的移动
        public void LayoutMove()
        {

        }
        //ITrans接口，实现移动至指定点
        public void SetToPoint(Point3d point3d)
        {
            //构建移动向量
            Point3d[] fromPoint = groupBoundary.Value.GetCorners();
            Transform trans;
            trans = Transform.Translation(point3d - fromPoint[0]);
            //逐一移动实体对象
            Move(trans);
        }
        //将对象拉入边界内
        public int DragIn(GH_Curve curve)
        {
            int failTime = 0;
            int limitation = 5;
            while ((Curve.PlanarCurveCollision(baseBoundary, curve.Value, Plane.WorldXY, 0.1)) && (failTime < limitation))
            {
                Point3d[] vetices = baseBoundary.GetBoundingBox(true).GetCorners();//获取场馆边界点
                List<Vector3d> vectors = new List<Vector3d>();//存储场馆与边线交点
                Vector3d move = Vector3d.Zero;//最终选用的移动变量
                for (int i = 0; i < vetices.Length; i++)
                {
                    if (SiteInfo.siteRetreat.Value.Contains(vetices[i]) == PointContainment.Outside)
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
                Move(Transform.Translation(move));
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
            Move(transform);
        }
        //旋转
        public void MustRotate(double angle)
        {
            Transform transform = Transform.Rotation(angle, groupBoundary.Value.Center);
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            hall.Transform(transform);
            for (int i = 0; i < theaterUnits.Count; i++)
            {
                theaterUnits[i].theaterUnit.Transform(transform);
            }
            baseCenter.Transform(transform);
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
            //创建游泳池边界
            GH_Rectangle court = new GH_Rectangle(new Rectangle3d(Plane.WorldXY, hallWidth,hallLength));
            List<GH_Rectangle> courts = new List<GH_Rectangle>();
            courts.Add(court);
            //获取辅助用房
            List<GH_Box> gymAuxiliaries = new List<GH_Box>();
            for (int i = 0; i < theaterUnits.Count; i++)
            {
                gymAuxiliaries.Add(theaterUnits[i].theaterUnit);
            }
            //构造新的简化场馆
            return new SimplifiedBuilding(buildingType, groupBoundary.Value.Center, groupBoundary, gymAuxiliaries, courts);
        }
    }
}
