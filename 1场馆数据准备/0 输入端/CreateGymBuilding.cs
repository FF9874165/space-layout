using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Space_Layout
{
    public class CreateGymBuilding : GH_Component
    {
        public CreateGymBuilding()
          : base("创建健身馆", "健身馆",
              "初始化健身馆信息，生成健身场地及其附属用房初始排布",
              "建筑空间布局", "数据预处理")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("是否设置此馆", "是否设置", "本项目是否包含此馆", GH_ParamAccess.item);
            pManager.AddNumberParameter("场馆面积", "场馆总建筑面积", "此类场地合计面积", GH_ParamAccess.item);
            pManager.AddBooleanParameter("是否设置专属辅助用房", "是否设置独立辅助用房", "此类场地是否是设置专属辅助用房的独立场馆；或与其他馆公用附属用房", GH_ParamAccess.item);
            pManager.AddNumberParameter("场地与辅助用房比", "运动场地与辅助用房面积比", "运动场地与辅助用房的面积比，此类场馆各单体该值一致", GH_ParamAccess.item);
            pManager.AddPointParameter("陈列基点", "展示基点", "此类球场陈列基点", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBoxParameter("场馆边界", "场馆单体", "场馆单体的空间范围", GH_ParamAccess.list);
            pManager.AddBoxParameter("球场", "运动场地", "运动场地的空间范围", GH_ParamAccess.list);
            pManager.AddBoxParameter("辅助用房", "辅助用房", "辅助用房的空间范围", GH_ParamAccess.list);
            pManager.AddGenericParameter("建筑场馆", "健身馆", "本项目中包括的建筑场馆类型", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //注册函数到重新生成的事件+
            Manager.restartEvent += Restart;

            //数据初始化
            bool isValid = false;
            double area = 0;
            bool hasAuxiliary = false;
            double courtRatio = 0;
            Point3d origin = new Point3d();

            if (!DA.GetData(0, ref isValid)) return;
            if (!DA.GetData(1, ref area)) return;
            if (!DA.GetData(2, ref hasAuxiliary)) return;
            if (!DA.GetData(3, ref courtRatio)) return;
            if (!DA.GetData(4, ref origin)) return;


            if (isValid)
            {
                //获取1个全民健身馆，进行该类场馆总体数据管理、行为协调
                Gymnasium gymnasium = new Gymnasium(area, hasAuxiliary, courtRatio, origin);

                List<GH_Box> building = new List<GH_Box>();
                List<GH_Box> court = new List<GH_Box>();
                List<GH_Box> auxiliary = new List<GH_Box>();

                for (int i = 0; i < gymnasium.gymnasiumGroup.Count; i++)
                {
                    building.Add(gymnasium.gymnasiumGroup[i].groupBoundary);
                    court.Add(gymnasium.gymnasiumGroup[i].gymCourt.gymCourt);
                    if (hasAuxiliary)
                    {
                        for (int j = 0; j < gymnasium.gymnasiumGroup[i].gymAuxiliaryGroup.auxiliary.Count; j++)
                        {
                            auxiliary.Add(gymnasium.gymnasiumGroup[i].gymAuxiliaryGroup.auxiliary[j].auxiliaryUnit);
                        }
                    }
                }
                DA.SetDataList(0, building);
                DA.SetDataList(1, court);
                DA.SetDataList(2, auxiliary);
                DA.SetData(3, gymnasium);
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
        protected override System.Drawing.Bitmap Icon => Properties.Resources._7_体操馆;

        public override Guid ComponentGuid
        {
            get { return new Guid("8B359B77-B4EF-41FD-B1E8-E7D571A7E532"); }
        }
    }
}