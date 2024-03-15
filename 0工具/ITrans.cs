using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //建筑场馆单体需要实现的接口
    public interface ITrans
    {
        //属性
        //获取朝向
        Orientation Orientation { get; }
        //获取楼层位置
        int CurrentLevel
        {
            get; set;
        }
        //获取该层编号位置
        int ItemIndex
        {
            get; set;
        }

        //方法
        //将场馆拖入限制边界内
        int DragIn(GH_Curve curve);
        //获取水平/垂直方向场馆跨度的一半
        double GetHalfBaseSize(int orientation, GH_Box groupB);
        //数据准备阶段将场馆移动至展示位置
        void LayoutMove();
        //首层布局之初，将场馆放置到限制边界内的随机点
        void SetToPoint(Point3d point3d);
        //朝向
        void SetOrientation();
        //场馆单体移动
        void Trans(Transform transform);
        //场馆单体旋转
        void MustRotate(double angle);
        //提供场馆单体外轮廓box
        GH_Box GetBoundaryBox();
        //获取场馆单体中心点
        Point3d GetCenterPoint();
        //创建写入JSON用的简化场馆对象
        SimplifiedBuilding CreateSimplifiedBuilding();
    }
}
