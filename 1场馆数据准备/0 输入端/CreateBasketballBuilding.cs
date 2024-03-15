using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Space_Layout
{
    public class CreateBasketballBuilding : GH_Component
    {
        public CreateBasketballBuilding()
          : base("创建篮球比赛馆", "篮球比赛",
              "初始化篮球比赛馆信息，生成球场、坐席及其附属用房初始排布",
              "建筑空间布局", "数据预处理")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("是否设置此馆", "是否设置", "本项目是否包含此馆", GH_ParamAccess.item);
            pManager.AddNumberParameter("场馆面积", "场馆总建筑面积", "此类场地合计面积", GH_ParamAccess.item);
            pManager.AddNumberParameter("球场划分率", "运动场地与辅助用房面积比", "此类球场将辅助用房划分一部分给运动场地的比例", GH_ParamAccess.item);
            pManager.AddIntegerParameter("观众人数", "观众人数", "观赛最大人数", GH_ParamAccess.item);
            pManager.AddPointParameter("陈列基点", "展示基点", "此类球场陈列基点", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBoxParameter("场馆边界", "场馆单体", "场馆单体的空间范围", GH_ParamAccess.item);
            pManager.AddRectangleParameter("球场边界", "运动场地", "运动场地的空间范围", GH_ParamAccess.list);
            pManager.AddRectangleParameter("坐席边界", "坐席", "坐席的空间范围", GH_ParamAccess.list);
            pManager.AddBoxParameter("辅助用房", "辅助用房", "辅助用房的空间范围", GH_ParamAccess.list);
            pManager.AddGenericParameter("篮球比赛馆类", "篮球比赛馆", "本项目中包括的篮球比赛馆类型", GH_ParamAccess.item);
            pManager.AddCurveParameter("底面", "底面", "", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            #region 数据准备
            //注册函数到重新生成的事件
            Manager.restartEvent += Restart;

            //变量声明
            bool IsValid = false;
            double area = 0;
            double reductionRatio = 0;
            int spectator = 0;
            Point3d origin = new Point3d();

            if (!DA.GetData(0, ref IsValid)) return;
            if (!DA.GetData(1, ref area)) return;
            if (!DA.GetData(2, ref reductionRatio)) return;
            if (!DA.GetData(3, ref spectator)) return;
            if (!DA.GetData(4, ref origin)) return;
            #endregion

            if (IsValid)//若本项目设置此类场馆
            {
                //构建比赛馆对象
                BasketballMatchBuilding basketballMatchBuilding = new BasketballMatchBuilding(spectator, area, origin, reductionRatio);
                List<GH_Rectangle> courts = new List<GH_Rectangle>();
                List<GH_Rectangle> seats = new List<GH_Rectangle>();
                List<GH_Box> auxiliaries = new List<GH_Box>();
                Curve boundaryCurve=basketballMatchBuilding.baseBoundary;

                //获取球场
                courts.Add(basketballMatchBuilding.courtActual);
                courts.Add(basketballMatchBuilding.court);

                //获取坐席
                for (int i = 0; i < basketballMatchBuilding.seats.Count; i++)
                {
                    seats.Add(basketballMatchBuilding.seats[i]);
                }

                //获取辅助空间
                for (int i = 0; i < basketballMatchBuilding.auxiliary.Count; i++)
                {
                    auxiliaries.Add(basketballMatchBuilding.auxiliary[i].auxiliaryUnit);
                }

                //输出变量
                DA.SetData(0, basketballMatchBuilding.groupBoundary);
                DA.SetDataList(1, courts);
                DA.SetDataList(2, seats);
                DA.SetDataList(3, auxiliaries);
                DA.SetData(4, basketballMatchBuilding);
                DA.SetData(5, boundaryCurve);
            }
        }
        //Manager监听的方法，当本轮生成结束后调用
        public void Restart()
        {
            OnPingDocument().ScheduleSolution(StaticObject.timeSpan, new GH_Document.GH_ScheduleDelegate(this.ScheduleCallback));
        }
        //重启下一轮计算的方法
        private void ScheduleCallback(GH_Document doc)
        {
            ExpireSolution(false);
        }
        protected override System.Drawing.Bitmap Icon => Properties.Resources._4_篮球比赛;
        public override Guid ComponentGuid
        {
            get { return new Guid("1ABE11B4-5BA5-4AE0-95AC-75F22294A7A9"); }
        }
    }
}