using System;
using System.Collections.Generic;
using VRageMath;

namespace TargetTrackerTests
{
    /// <summary>
    /// 轨迹生成器 - 生成各种测试轨迹
    /// </summary>
    public class 轨迹生成器
    {
        /// <summary>
        /// 生成直线运动轨迹
        /// </summary>
        /// <param name="起点">起始位置</param>
        /// <param name="速度">运动速度向量</param>
        /// <param name="总时长秒">总时长（秒）</param>
        /// <param name="采样间隔ms">采样间隔（毫秒）</param>
        public static List<轨迹点> 生成直线轨迹(Vector3D 起点, Vector3D 速度, double 总时长秒, long 采样间隔ms)
        {
            var 轨迹 = new List<轨迹点>();
            long 总时长ms = (long)(总时长秒 * 1000);
            
            for (long t = 0; t <= 总时长ms; t += 采样间隔ms)
            {
                double 时间秒 = t * 0.001;
                Vector3D 位置 = 起点 + 速度 * 时间秒;
                轨迹.Add(new 轨迹点(位置, 速度, Vector3D.Zero, t));
            }
            
            return 轨迹;
        }

        /// <summary>
        /// 生成二次曲线运动轨迹（匀加速）
        /// </summary>
        /// <param name="起点">起始位置</param>
        /// <param name="初速度">初始速度向量</param>
        /// <param name="加速度">加速度向量</param>
        /// <param name="总时长秒">总时长（秒）</param>
        /// <param name="采样间隔ms">采样间隔（毫秒）</param>
        public static List<轨迹点> 生成二次曲线轨迹(Vector3D 起点, Vector3D 初速度, Vector3D 加速度, double 总时长秒, long 采样间隔ms)
        {
            var 轨迹 = new List<轨迹点>();
            long 总时长ms = (long)(总时长秒 * 1000);
            
            for (long t = 0; t <= 总时长ms; t += 采样间隔ms)
            {
                double 时间秒 = t * 0.001;
                Vector3D 位置 = 起点 + 初速度 * 时间秒 + 0.5 * 加速度 * 时间秒 * 时间秒;
                Vector3D 速度 = 初速度 + 加速度 * 时间秒;
                轨迹.Add(new 轨迹点(位置, 速度, 加速度, t));
            }
            
            return 轨迹;
        }

        /// <summary>
        /// 生成圆周运动轨迹
        /// </summary>
        /// <param name="圆心">圆心位置</param>
        /// <param name="半径">圆周半径</param>
        /// <param name="角速度">角速度（弧度/秒）</param>
        /// <param name="平面法向量">圆周所在平面的法向量（需归一化）</param>
        /// <param name="起始方向">起始位置方向向量（相对圆心，需归一化）</param>
        /// <param name="总时长秒">总时长（秒）</param>
        /// <param name="采样间隔ms">采样间隔（毫秒）</param>
        public static List<轨迹点> 生成圆周运动轨迹(
            Vector3D 圆心, 
            double 半径, 
            double 角速度, 
            Vector3D 平面法向量, 
            Vector3D 起始方向,
            double 总时长秒, 
            long 采样间隔ms)
        {
            var 轨迹 = new List<轨迹点>();
            long 总时长ms = (long)(总时长秒 * 1000);
            
            // 归一化向量
            平面法向量 = Vector3D.Normalize(平面法向量);
            起始方向 = Vector3D.Normalize(起始方向);
            
            for (long t = 0; t <= 总时长ms; t += 采样间隔ms)
            {
                double 时间秒 = t * 0.001;
                double 角度 = 角速度 * 时间秒;
                
                // 使用四元数旋转起始方向向量
                QuaternionD 旋转 = QuaternionD.CreateFromAxisAngle(平面法向量, 角度);
                Vector3D 当前方向 = Vector3D.Transform(起始方向, 旋转);
                Vector3D 位置 = 圆心 + 当前方向 * 半径;
                
                // 速度垂直于半径方向
                Vector3D 速度方向 = Vector3D.Cross(平面法向量, 当前方向);
                Vector3D 速度 = Vector3D.Normalize(速度方向) * (半径 * 角速度);
                
                // 向心加速度（指向圆心）
                Vector3D 加速度 = -当前方向 * (角速度 * 角速度 * 半径);
                
                轨迹.Add(new 轨迹点(位置, 速度, 加速度, t));
            }
            
            return 轨迹;
        }

        /// <summary>
        /// 生成正弦运动轨迹（沿一个轴匀速运动，另一个轴正弦振荡）
        /// </summary>
        /// <param name="起点">起始位置</param>
        /// <param name="前进速度">沿前进方向的匀速速度（如X轴）</param>
        /// <param name="振幅">正弦振荡的振幅（米）</param>
        /// <param name="频率">正弦振荡的频率（Hz）</param>
        /// <param name="振荡轴">振荡方向（需归一化，如Y轴）</param>
        /// <param name="总时长秒">总时长（秒）</param>
        /// <param name="采样间隔ms">采样间隔（毫秒）</param>
        public static List<轨迹点> 生成正弦运动轨迹(
            Vector3D 起点,
            Vector3D 前进速度,
            double 振幅,
            double 频率,
            Vector3D 振荡轴,
            double 总时长秒,
            long 采样间隔ms)
        {
            var 轨迹 = new List<轨迹点>();
            long 总时长ms = (long)(总时长秒 * 1000);
            
            振荡轴 = Vector3D.Normalize(振荡轴);
            double 角频率 = 2 * Math.PI * 频率; // ω = 2πf
            
            for (long t = 0; t <= 总时长ms; t += 采样间隔ms)
            {
                double 时间秒 = t * 0.001;
                
                // 位置 = 起点 + 前进位移 + 正弦振荡位移
                // 正弦位移: A * sin(ωt)
                double 正弦位移 = 振幅 * Math.Sin(角频率 * 时间秒);
                Vector3D 位置 = 起点 + 前进速度 * 时间秒 + 振荡轴 * 正弦位移;
                
                // 速度 = 前进速度 + 正弦振荡速度
                // 正弦速度: A * ω * cos(ωt)
                double 正弦速度分量 = 振幅 * 角频率 * Math.Cos(角频率 * 时间秒);
                Vector3D 速度 = 前进速度 + 振荡轴 * 正弦速度分量;
                
                // 加速度 = 正弦振荡加速度
                // 正弦加速度: -A * ω² * sin(ωt)
                double 正弦加速度分量 = -振幅 * 角频率 * 角频率 * Math.Sin(角频率 * 时间秒);
                Vector3D 加速度 = 振荡轴 * 正弦加速度分量;
                
                轨迹.Add(new 轨迹点(位置, 速度, 加速度, t));
            }
            
            return 轨迹;
        }

        /// <summary>
        /// 生成螺旋运动轨迹（圆周+轴向运动）
        /// </summary>
        public static List<轨迹点> 生成螺旋轨迹(
            Vector3D 起点,
            double 半径,
            double 角速度,
            Vector3D 轴向速度,
            Vector3D 平面法向量,
            double 总时长秒,
            long 采样间隔ms)
        {
            var 轨迹 = new List<轨迹点>();
            long 总时长ms = (long)(总时长秒 * 1000);
            
            平面法向量 = Vector3D.Normalize(平面法向量);
            
            // 找一个垂直于法向量的起始方向
            Vector3D 起始方向 = Vector3D.CalculatePerpendicularVector(平面法向量);
            起始方向 = Vector3D.Normalize(起始方向);
            
            for (long t = 0; t <= 总时长ms; t += 采样间隔ms)
            {
                double 时间秒 = t * 0.001;
                double 角度 = 角速度 * 时间秒;
                
                // 圆周部分
                QuaternionD 旋转 = QuaternionD.CreateFromAxisAngle(平面法向量, 角度);
                Vector3D 当前方向 = Vector3D.Transform(起始方向, 旋转);
                Vector3D 圆周位置 = 当前方向 * 半径;
                
                // 加上轴向位移
                Vector3D 位置 = 起点 + 圆周位置 + 轴向速度 * 时间秒;
                
                // 速度 = 圆周切线速度 + 轴向速度
                Vector3D 切线速度 = Vector3D.Cross(平面法向量, 当前方向);
                切线速度 = Vector3D.Normalize(切线速度) * (半径 * 角速度);
                Vector3D 速度 = 切线速度 + 轴向速度;
                
                // 加速度只有向心加速度
                Vector3D 加速度 = -当前方向 * (角速度 * 角速度 * 半径);
                
                轨迹.Add(new 轨迹点(位置, 速度, 加速度, t));
            }
            
            return 轨迹;
        }
    }

    /// <summary>
    /// 轨迹点数据结构
    /// </summary>
    public struct 轨迹点
    {
        public Vector3D 位置;
        public Vector3D 速度;
        public Vector3D 加速度;
        public long 时间戳ms;

        public 轨迹点(Vector3D 位置, Vector3D 速度, Vector3D 加速度, long 时间戳ms)
        {
            this.位置 = 位置;
            this.速度 = 速度;
            this.加速度 = 加速度;
            this.时间戳ms = 时间戳ms;
        }
    }
}
