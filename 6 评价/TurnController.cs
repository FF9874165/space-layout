using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Org.BouncyCastle.Utilities;
using Rhino.Geometry;

namespace Space_Layout
{
    //回合控制器
    public class TurnController : GH_Component
    {
        public TurnController()
          : base("回合控制器", "控制器",
              "管理各轮计算的开始与结束",
              "建筑空间布局", "方案评价")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("成功次数", "次数", "成功生成方案结果达到该次数后，计算停止", GH_ParamAccess.item);
            pManager.AddBooleanParameter("启用自动计算", "启用", "自动连续计算多轮方案布局，直至成功次数达到指定次数", GH_ParamAccess.item);
            pManager.AddBooleanParameter("初始化计算环境", "初始化", "初始化计算环境，重头开始计算", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("", "状态", "", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            #region 数据准备
            int successTimes = 5;//成功生成方案次数
            bool ifStart=false;//开始按钮
            bool ifReset = false;//重置按钮
            if (!DA.GetData(0, ref successTimes)) return;
            if (!DA.GetData(1, ref ifStart)) return;
            if (!DA.GetData(2, ref ifReset)) return;
            #endregion

            //达到进入下一轮条件,且未满足成功生成次数的要求
            if (((Manager.restart==true)||(Manager.ifEvaluateOK == true))&&(Manager.successTime<= successTimes)&&(ifStart==true))
            {
                if (ifReset==true)//整个运算重置
                {
                    Manager.runningTime = 0;
                    Manager.successTime = 0;
                    //获取需要重新启动的组件并开始下一轮
                    Manager.RestartManager();
                }
                else//不重置
                {
                    //若未达指定次数，则继续下一轮
                    if (Manager.successTime < successTimes)
                    {
                        //获取需要重新启动的组件并开始下一轮
                        Manager.RestartManager();
                    }
                }
                test = Manager.successTime.ToString();
            }

            //测试
            DA.SetData(0, test);
            
            //本组件自动刷新
            OnPingDocument().ScheduleSolution(10, new GH_Document.GH_ScheduleDelegate(this.ScheduleCallback));
        }
        
        //测试变量
        string test = "×";
        
        //刷新
        private void ScheduleCallback(GH_Document doc)
        {
            ExpireSolution(false);
        }
        protected override System.Drawing.Bitmap Icon => Properties.Resources._13_监测;

        public override Guid ComponentGuid
        {
            get { return new Guid("1C5D0365-D531-4D32-8969-255CF7974D71"); }
        }
    }
}