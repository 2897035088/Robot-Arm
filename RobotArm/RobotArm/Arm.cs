using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.Util;
using System.IO;
using System.Threading;

namespace RobotArm
{
    class Arm
    {
        //
        // 摘要:
        //     机械臂的大臂长度
        int L1 { set; get; }
        //
        // 摘要:
        //     机械臂的小臂长度
        int L2 { set; get; }
        //
        // 摘要:
        //     机械臂的大臂旋转角度
        double Ang1 { set; get; }
        //
        // 摘要:
        //     机械臂的小臂旋转角度
        double Ang2 { set; get; }
        //
        // 摘要:
        //     画机械臂的位图
        Bitmap Rbm { set; get; }
        //
        // 摘要:
        //     画机械臂末端轨迹的位图
        Bitmap Tbm { set; get; }

        #region 将直线细分成点集
        /// <summary>
        /// 将直线细分成点集。
        /// </summary>
        /// <param name="startx">起点的X坐标</param>
        /// <param name="starty">起点的Y坐标</param>
        /// <param name="endx">终点的x坐标</param>
        /// <param name="X">终点的Y坐标</param>
        public List<PointF> DrawLine(double startx, double starty, double endx, double endy)
        {
            double x, y;//用来表示下一个机械臂末端位置
            List<PointF> points = new List<PointF>();
            double r = Math.Sqrt((startx - endx) * (startx - endx) + (starty - endy) * (starty - endy));//直线长度
            for (int i = 0; i < r / 0.05; i++)//对所画图形进行分段计数
            {
                x = startx + i / (r / 0.05) * (endx - startx);
                y = starty + i / (r / 0.05) * (endy - starty);
                points.Add(new PointF((float)x, (float)y));
            }
            return points;
        }


        #endregion

        #region 将圆细分成点集
        /// <summary>
        /// 将圆细分成点集。
        /// </summary>
        /// <param name="startx">圆心的X坐标</param>
        /// <param name="starty">圆心的Y坐标</param>
        /// <param name="r">圆的半径</param>
        public List<PointF> DrawCircle(double startx, double starty, double r)
        {
            List<PointF> points = new List<PointF>();
            double x, y;
            double theta;//存储画圆时的弧度，取值范围（-PI,PI）
            double k = Math.PI * r / 0.05; //将圆的周长细分为2k个点
            for (int i = 0; i < 2 * k; i++)//从圆的最左点开始逆时针绘制，即从-PI到PI绘制
            {
                theta = (i - k) / k * Math.PI;//每个点对应的弧度
                //计算点的坐标
                x = startx + r * Math.Cos(theta);
                y = starty + r * Math.Sin(theta);
                points.Add(new PointF((float)x, (float)y));
            }
            return points;
        }
        #endregion

        #region 求解对应坐标下两臂转动角度
        /// <summary>
        /// 求解两臂转动角度,（反）三角函数都是弧度单位。
        /// </summary>
        /// <param name="X">目标点的Y坐标（对应我定义的坐标系）</param>
        /// <param name="Y">目标点的Y坐标（对应我定义的坐标系）</param>
        public void GetAngel(double X, double Y)
        {
            double x = X;
            double y = Y;
            double r = Math.Sqrt(x * x + y * y);//目标点到原点的距离
            double theta = Math.Atan2(y, x);//取值范围为（-PI,PI）
            double phi = Math.PI - Math.Acos((L1 * L1 + L2 * L2 - r * r) / (2 * L1 * L2));
            double theta1 = Math.Acos((r * r + L1 * L1 - L2 * L2) / (2 * L1 * r));
            if (r == 0)
            {
                Ang2 = Math.PI;
            }
            //两个解:（theta+theta1，-phi）、（theta-theta1，phi）
            //选解采用最短准则：即对应的两臂角度相对于上一时刻的两臂角度需要变动的角度和更小的解
            else
            {
                if ((Math.Abs(theta + theta1 - Ang1) + Math.Abs(-phi - Ang2)) > (Math.Abs(theta - theta1 - Ang1) + Math.Abs(phi - Ang2)))
                {
                    Ang1 = theta - theta1;
                    Ang2 = phi;
                }
                else
                {
                    Ang1 = theta + theta1;
                    Ang2 = -phi;
                }
            }
        }
        #endregion

        #region 绘制机械臂
        /// <summary>
        /// 机械臂的底端位于坐标系原点
        /// </summary>
        public void DrawMechanism()
        {
            //特别注意：我定义的坐标系为我们平常时常用的坐标系，
            //但电脑屏幕等运用的坐标系是：左上角为原点，水平向右为X正半轴，水平向下为Y正半轴
            //因此两个坐标系之间存在装换。 
            PointF pointO = new PointF(200F, 200F);//机械臂O点位置，位于坐标原点
            PointF pointA = new PointF((float)(200 + L1 * Math.Cos(Ang1)), (float)(200 - L1 * Math.Sin(Ang1)));//机械臂A点位置
            PointF pointB = new PointF((float)(200 + L1 * Math.Cos(Ang1) + L2 * Math.Cos(Ang1 + Ang2)), (float)(200 - L1 * Math.Sin(Ang1) - L2 * Math.Sin(Ang1 + Ang2)));//机械臂B点位置

            Graphics g = Graphics.FromImage(Rbm);//定义图像，图像为抽象类，不可直接构造函数生成
            //绘制斜线时消除锯齿（鼠标放在那就可以看到函数功能）
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SmoothingMode = SmoothingMode.HighQuality;
            Pen pen = new Pen(Color.Red, 1);//定义一只红色、1像素宽的笔
            g.DrawLine(pen, pointO, pointA);//绘制大臂（连接OA线段）
            g.DrawLine(pen, pointA, pointB);//绘制小臂（连接AB线段）
            //绘制转动副O
            g.DrawEllipse(pen, 200 - 5, 200 - 5, 10, 10);
            g.FillEllipse(new SolidBrush(Color.White), 200 - 5, 200 - 5, 10, 10);
            //绘制转动副A
            g.DrawEllipse(pen, (float)(200 + L1 * Math.Cos(Ang1)) - 5, (float)(200 - L1 * Math.Sin(Ang1)) - 5, 10, 10);
            g.FillEllipse(new SolidBrush(Color.White), (float)(200 + L1 * Math.Cos(Ang1)) - 5, (float)(200 - L1 * Math.Sin(Ang1)) - 5, 10, 10);//绘制转动副A

            //释放资源
            g.Dispose();
            pen.Dispose();
        }
        #endregion



        #region 延时函数
        /// <summary>
        /// 延时函数，单位为毫秒
        /// </summary>
        /// <param name="delayTime"></param>
        public void DelayMs(int delayTime)
        {
            DateTime now = DateTime.Now;
            int s;
            do
            {
                TimeSpan spand = DateTime.Now - now;
                s = spand.Milliseconds;
                Application.DoEvents();
            }
            while (s < delayTime);
        }
        #endregion
    }
}
