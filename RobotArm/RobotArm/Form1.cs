using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Drawing.Drawing2D;
using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.Util;
using System.IO;

namespace RobotArm
{
    public partial class Form1 : Form
    {
        #region 全局变量
        Arm arm = new Arm();
        double startX;   // 用来存储运动开始的位置
        double startY;
        double endX = 200;//用来存储运动结束的位置
        double endY = 0;
        string graph;//用来标志画直线还是画圆
        PointF pointLast;//机械臂末端上一时刻位置
        PointF pointNow;//机械臂末端现在位置
        VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();//创建用于存储轮廓的VectorOfVectorOfPoint数据类型（命名空间Emgu.CV.Util）
        #endregion

        public Form1()
        {
            InitializeComponent();
        }

        //在PictureBox1的背景图片中作画，在图片中显示机械臂
        private void Form1_Load(object sender, EventArgs e)
        {
            arm.L1 = Convert.ToInt16(textBox6.Text);
            arm.L2 = Convert.ToInt16(textBox7.Text);
            arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
            arm.Tbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
            arm.DrawMechanism();
            pictureBox1.Image = arm.Rbm;
        }

        //图像在进行高斯滤波时调整Canny检测算子的下边缘，可以得到不同轮廓
        //private void numericUpDown_ValueChanged(object sender, EventArgs e)
        //{//调整Canny检测算子的下边缘，得到不同的轮廓
        //    Mat _outputmat = new Mat();
        //    CvInvoke.Canny(imageBox1.Image, _outputmat, Convert.ToInt16(numericUpDown1.Value), Convert.ToInt16(numericUpDown2.Value));
        //    imageBox2.Image = _outputmat;
        //    imageBox2.Refresh();
        //    CvInvoke.FindContours(_outputmat, contours, null, Emgu.CV.CvEnum.RetrType.External,
        //            Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
        //}

        //定时器绘制末端轨迹

        private void timer1_Tick(object sender, EventArgs e)
        {
            //机械臂现在末端位置
            pointNow = new PointF((float)(200 + arm.L1 * Math.Cos(arm.Ang1) + arm.L2 * Math.Cos(arm.Ang1 + arm.Ang2)), (float)(200 - arm.L1 * Math.Sin(arm.Ang1) - arm.L2 * Math.Sin(arm.Ang1 + arm.Ang2)));
            if (pointLast == new PointF(0, 0))
            {
                pointLast = pointNow;//如果是绘制曲线的起点，便没有pointLast，令其就等与pointNow
            }
            arm.DrawTrajectory(pointLast, pointNow);//绘制机械臂末端前一时刻和当前时刻间的线段，视为轨迹
            pictureBox1.BackgroundImage = arm.Tbm;//将轨迹显示在BackgroundImage中，保留了之前轨迹。
            pointLast = pointNow;//更新上一次机械臂末端位置，
        }

        //实时显示角度
        private void timer2_Tick(object sender, EventArgs e)
        {
            textBox8.Text = (arm.Ang1 / Math.PI * 180).ToString();
            textBox9.Text = (arm.Ang2 / Math.PI * 180).ToString();
        }

        //画直线按钮
        private void button1_Click(object sender, EventArgs e)
        {//“画直线”按钮的点击事件
            groupBox1.Visible = true;//坐标输入功能开启
            groupBox2.Visible = false;//写字输入功能关闭
            textBox4.Visible = true;
            label9.Text = "起点：";
            label10.Text = "终点：";
            graph = "Line";//标志其为画直线
        }

        //画圆按钮
        private void button2_Click(object sender, EventArgs e)
        {//“画圆”按钮的点击事件
            groupBox1.Visible = true;//坐标输入功能开启
            groupBox2.Visible = false;//写字输入功能关闭
            textBox4.Visible = false;
            label9.Text = "圆心：";
            label10.Text = "半径：";
            graph = "Circle";//标志其为画圆
        }

        //画轮廓按钮
        private void button3_Click(object sender, EventArgs e)
        {
            IOutputArray hierarchy = null;//与contours对应的向量，hierarchy[i][0]~hierarchy[i][3]存储后、前、子、父级轮廓链表表头
            OpenFileDialog ofd = new OpenFileDialog();//创建一个对话框，选择需要画轮廓的图片
            ofd.Filter = "JPG图片|*.jpg|BMP图片|*.bmp";//选择文件的类型（filter：过滤）
            
            //处理图像，得到图像的轮廓信息！
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Mat _inputmat = new Mat(ofd.FileName);
                imageBox1.Image = _inputmat;//读入图像，在ImageBox中显示
                CvInvoke.GaussianBlur(_inputmat, _inputmat, new Size(3, 3), 3, 3);//对输入图像进行高斯滤波，并将滤波后的图像存至_inputmat
                Mat dst = new Mat();//存储图片轮廓信息
                CvInvoke.Canny(_inputmat, dst, 120, 180);//Canny 边缘检测算子
                imageBox2.Image = dst;//显示轮廓图片

                #region  CvInvoke.FindContours方法参数讲解
                ///<summary>
                ///IOutputArray contours：检测到的轮廓。通常使用VectorOfVectorOfPoint类型。
                ///IOutputArray hierarchy：可选的输出向量,包含图像的拓扑信息。不使用的时候可以用 null 填充。
                ///每个独立的轮廓（连通域）对应 4 个 hierarchy元素 hierarchy[i][0]~hierarchy[i][4]
                ///（i表示独立轮廓的序数）分别表示后一个轮廓、前一个轮廓、父轮廓、子轮廓的序数。

                ///RetrType mode标识符及其解析：
                ///External = 0 提取的最外层轮廓；
                ///List = 1 提取所有轮廓
                ///Ccomp = 2 检索所有轮廓并将它们组织成两级层次结构:水平是组件的外部边界,二级约束边界的洞。
                ///Tree = 3 提取所有的轮廓和建构完整的层次结构嵌套的轮廓。

                ///ChainApproxMethod表示轮廓的逼近方法
                ///ChainCode = 0 Freeman链码输出轮廓。所有其他方法输出多边形(顶点序列)。
                ///ChainApproxNone = 1 所有的点从链代码转化为点;
                ///ChainApproxSimple = 2 压缩水平、垂直和对角线部分,也就是说, 只剩下他们的终点;
                ///ChainApproxTc89L1 = 3 使用The - Chinl 链逼近算法的一个
                ///ChainApproxTc89Kcos = 4 使用The - Chinl 链逼近算法的一个
                ///LinkRuns = 5, 使用完全不同的轮廓检索算法通过链接的水平段的1s轨道。
                ///用这种方法只能使用列表检索模式。
                ///</summary>
                #endregion

                CvInvoke.FindContours(dst, contours, hierarchy, Emgu.CV.CvEnum.RetrType.External,
                    Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
            }
            graph = "Graph";
        }
        
        //写字按钮
        private void button4_Click(object sender, EventArgs e)
        {
            groupBox1.Visible = false;
            groupBox2.Visible = true;
            graph = "Font";
        }

        //开始绘制按钮
        private void button5_Click(object sender, EventArgs e)
        {
            //读取设置的两臂长度
            arm.L1 = Convert.ToInt16(textBox6.Text);
            arm.L2 = Convert.ToInt16(textBox7.Text);

            //绘制图像为直线
            if (graph == "Line")
            {
                //将上一次的机械臂的末端位置（即现在机械臂末端位置）设置为起点位置
                startX = endX;
                startY = endY;
                //将绘制目标图像（即线段）的起点位置设置为终点位置
                endX = double.Parse(textBox1.Text);
                endY = double.Parse(textBox2.Text);
                List<PointF> points = arm.DrawLine(startX, startY, endX, endY);//直线细分后的点集
                foreach (PointF point in points)
                {
                    arm.GetAngle(point.X, point.Y);//对每个点求解对应的大小臂旋转角度
                    //创建新的位图，更新机械臂位置（可以直接用图像的Clear（Color.White）方法，但是个人不喜欢这个方法）
                    arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                    arm.DrawMechanism();//根据大小臂旋转角度绘制机械臂
                    pictureBox1.Image = arm.Rbm;//将更新后的机械臂设置为pictureBox的Image
                    arm.DelayMs(4);//延时，使人眼可以看到。没有延时就无法看到中间过程
                }
                //将上一次的机械臂的末端位置（即上面说的目标图像的起点位置）设置为起点位置
                startX = endX;
                startY = endY;
                //将绘制目标图像的终点位置设置为终点位置
                endX = double.Parse(textBox3.Text);
                endY = double.Parse(textBox4.Text);
                points = arm.DrawLine(startX, startY, endX, endY);
                timer1.Enabled = true;
                foreach (PointF point in points)
                {
                    arm.GetAngle(point.X, point.Y);
                    arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                    arm.DrawMechanism();
                    pictureBox1.Image = arm.Rbm;
                    arm.DelayMs(4);
                }
                timer1.Enabled = false;
            }
            //绘制图像为圆
            if (graph == "Circle")
            {
                //将上一次的机械臂的末端位置（即现在机械臂末端位置）设置为起点位置
                startX = endX;
                startY = endY;
                //将绘制目标图像（即圆）的起点位置（即圆的最左点）设置为终点位置
                endX = double.Parse(textBox1.Text) - double.Parse(textBox3.Text);
                endY = double.Parse(textBox2.Text);
                List<PointF> points = arm.DrawLine(startX, startY, endX, endY);//直线细分后的点集
                foreach (PointF point in points)
                {
                    arm.GetAngle(point.X, point.Y);
                    arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                    arm.DrawMechanism();
                    pictureBox1.Image = arm.Rbm;
                    arm.DelayMs(4);
                }
                points = arm.DrawCircle(double.Parse(textBox1.Text), double.Parse(textBox2.Text), double.Parse(textBox3.Text));
                timer1.Enabled = true;
                foreach (PointF point in points)
                {
                    arm.GetAngle(point.X, point.Y);
                    arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                    arm.DrawMechanism();
                    pictureBox1.Image = arm.Rbm;
                    arm.DelayMs(4);
                }
                timer1.Enabled = false;
                
            }
            //绘制图像为人像轮廓
            if (graph == "Graph")
            {//轮廓
                for (int i = 0; i < contours.Size; i++)
                {
                    //将上一次的机械臂的末端位置（即现在机械臂末端位置）设置为起点位置
                    startX = endX;
                    startY = endY;
                    //将绘制目标图像（即轮廓的第i组的第j条线段）的起点位置（即圆的最左点）设置为终点位置
                    endX = contours[i][0].X - 40;
                    endY = -(contours[i][0].Y - 125);
                    List<PointF> points = arm.DrawLine(startX, startY, endX, endY);//直线细分后的点集
                    foreach (PointF point in points)
                    {
                        arm.GetAngle(point.X, point.Y);
                        arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                        arm.DrawMechanism();
                        pictureBox1.Image = arm.Rbm;
                        arm.DelayMs(4);
                    }
                    for (int j = 0; j < contours[i].Size - 1; j++)
                    {
                        //将上一次的机械臂的末端位置（即上面说的轮廓的第i组的第j条线段的起点位置）设置为起点位置
                        startX = endX;
                        startY = endY;
                        //将机械臂的目标末端位置（即上面说的轮廓的第i组的第j条线段的终点位置）设置为起点位置
                        endX = contours[i][j + 1].X - 40;
                        endY = -(contours[i][j + 1].Y - 125);
                        //然后绘制轮廓的第i组第j条线段
                        points = arm.DrawLine(startX, startY, endX, endY);
                        timer1.Enabled = true;
                        foreach (PointF point in points)
                        {
                            arm.GetAngle(point.X, point.Y);
                            arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                            arm.DrawMechanism();
                            pictureBox1.Image = arm.Rbm;
                            arm.DelayMs(4);
                        }
                        timer1.Enabled = false;
                    }
                    pointLast = new PointF(0, 0);//每一次图像绘制成功后，将pointLast设置（0，0）（与定时器配合使用），自行体会作用，感觉讲不清
                }
            }
            //绘制图像为汉字
            if (graph == "Font")
            {
                double size = Convert.ToDouble(textBox10.Text);
                for (int j = 0; j < textBox5.Text.Length; j++)
                {
                    char word = textBox5.Text[j];//取出第j个文字
                    //将这个汉字转化为GB22313编码，并且加0构成文件名
                    byte[] wordbytes = Encoding.GetEncoding("GB2312").GetBytes(new char[] { word });
                    string filename = "0" + Convert.ToString((wordbytes[0] << 8) + wordbytes[1], 16);
                    //从文件中读出数据
                    StreamReader sr = new StreamReader("E:/下载软件/Visual Studio 2017/C#文件/Robotic Arm/汉字库/" + filename + ".txt");
                    string wordtxt = sr.ReadToEnd();//将文字所有信息读入wordtxt变量
                    string font = wordtxt.Split(new string[] { "7,-114,5,", "7,-113,0" }, StringSplitOptions.RemoveEmptyEntries)[1];//切取有用片段
                    string[] fontarray = font.Split(new string[] { ",", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);//将字符串转化为字符数组，去掉逗号空格这些
                    int i = 0;
                    //调整字的初始位置，即字的左下角坐标
                    Point point = new Point((int)(80 * j * size - 80), 0);
                    //将上一次机械臂末端位置设置为起点位置
                    startX = endX;
                    startY = endY;
                    //将字的左下角位置设置为机械臂末端目标位置
                    endX = point.X;
                    endY = point.Y;
                    List<PointF> points = arm.DrawLine(startX, startY, endX, endY);//直线细分后的点集
                    foreach (PointF p in points)
                    {
                        arm.GetAngle(p.X, p.Y);
                        arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                        arm.DrawMechanism();
                        pictureBox1.Image = arm.Rbm;
                        arm.DelayMs(4);
                    }
                    //解码文字信息，结合Shape文件格式解码出想要的数据
                    while (i < fontarray.Length)
                    {
                        if (fontarray[i] == "2")//说明为抬笔过程，就不用画轨迹，故为false
                        {
                            startX = endX;
                            startY = endY;
                            endX = startX + size * Convert.ToInt16(fontarray[i + 2]);
                            endY = startY + size * Convert.ToInt16(fontarray[i + 3]);
                            points = arm.DrawLine(startX, startY, endX, endY);//直线细分后的点集
                            foreach (PointF p in points)
                            {
                                arm.GetAngle(p.X, p.Y);
                                arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                                arm.DrawMechanism();
                                pictureBox1.Image = arm.Rbm;
                                arm.DelayMs(4);
                            }
                            i = i + 4;
                        }
                        if (fontarray[i++] == "1")//说明为落笔过程，需要画轨迹，故为true
                        {
                            for (; i < fontarray.Length && fontarray[i] != "2";)
                            {
                                if (fontarray[i] == "8")
                                {
                                    startX = endX;
                                    startY = endY;
                                    endX = startX + size * Convert.ToInt16(fontarray[i + 1]);
                                    endY = startY + size * Convert.ToInt16(fontarray[i + 2]);
                                    points = arm.DrawLine(startX, startY, endX, endY);//直线细分后的点集
                                    timer1.Enabled = true;
                                    foreach (PointF p in points)
                                    {
                                        arm.GetAngle(p.X, p.Y);
                                        arm.Rbm = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                                        arm.DrawMechanism();
                                        pictureBox1.Image = arm.Rbm;
                                        arm.DelayMs(4);
                                    }
                                    timer1.Enabled = false;
                                    i = i + 3;
                                }
                                else if (fontarray[i] == "12")//因为用到圆弧的字较少，所以忽略
                                {}
                            }
                            pointLast = new PointF(0, 0);//每一笔绘制成功后，将pointLast设置（0，0）（与定时器配合使用），自行体会作用
                        }
                    }
                }
            }
            pointLast = new PointF(0, 0);

        }
    }
}
