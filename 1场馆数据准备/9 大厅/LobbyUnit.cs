using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Types;
using Space_Layout;
using Rhino.Geometry;
using Rhino.Geometry.Collections;

namespace Space_Layout
{
    //大厅单体
    public class LobbyUnit : ITrans
    {
        #region 变量
        //内部变量
        public int groupNumber;
        public double width;
        public double length;
        public double height;
        public double area;//实际值
        public double rotation;//旋转角度
        public Orientation orientationBox;//朝向

        public GH_Box groupBoundary;//实体
        public Curve baseBoundary;//基底范围线
        public double baseArea;//基底面积
        public Curve ceilingBoundary;//屋面offset后范围线
        public double ceilingArea;//屋面offset后面积
        public Point3d baseCenter;//底面中心店，用于计算场馆间位置关系

        public int currentLevel = -1;//布局完成后，场馆所在楼层
        public int itemIndex = -1;//布局完成后，场馆在楼层的第几个
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
        //获取朝向
        public Orientation Orientation => orientationBox;
        #endregion
        
        //构造函数
        public LobbyUnit(int groupNumber)
        {
            this.groupNumber = groupNumber;
            if (StaticObject.lobby.isOnlyOne)//本项目仅1个大厅时
            {
                area = StaticObject.lobby.totalArea;
                StaticObject.lobby.areaBase.Add(area);//更新基底面积
                width = Math.Sqrt(StaticObject.lobby.totalArea / 0.3);
                length = area / width;
                height = 12;
            }
            else//多个大厅时，首先建立1F的第一个大厅，由于重要，所以面积占比较大
            {
                for (int i = 0; i < StaticObject.lobby.count; i++)
                {
                    area = StaticObject.lobby.area / StaticObject.lobby.count;
                    width = Math.Sqrt(area / 0.3);
                }
                StaticObject.lobby.areaBase.Add(area);//更新基底面积
                length = area / width;
                height = 12;
            }
            DrawLobbyUnit(width, length, height);
            groupBoundary.Transform(Transform.Rotation(rotation, groupBoundary.Boundingbox.Min));
            GetOutline();//更新基地、顶板扩展轮廓及数据
        }

        //创建lobby实体
        public void DrawLobbyUnit(double w, double l, double h)
        {
            groupBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, w), new Interval(0, l), new Interval(0, h)));
            //Tool.IFRotateHalfPie(ref rotation, 0.5);
        }
        //更新相关边界、指标
        public void GetOutline()
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
            baseBoundary.Transform(Transform.Translation(-Vector3d.ZAxis * height));
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
            baseCenter.Transform(transform);
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
            //构造新的简化场馆
            return new SimplifiedBuilding(BuildingType.大厅, groupBoundary.Value.Center, groupBoundary,new List<GH_Box>(),new List<GH_Rectangle>());
        }
    }
}
