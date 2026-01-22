using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRageMath;
using Sandbox.ModAPI.Ingame;
using IngameScript;  // 引用 TargetTracker 和 SimpleTargetInfo 所在的命名空间

namespace IngameScript
{
    // 为测试环境提供 MyGridProgram 的空实现
    public class MyGridProgram
    {
        // 空实现，仅用于编译
    }
}

namespace TargetTrackerTests
{
    /// <summary>
    /// 测试结果数据
    /// </summary>
    public class 测试结果
    {
        public string 测试名称;
        public List<轨迹点> 真实轨迹;
        public List<预测结果点> 预测结果;
        public 性能统计 统计;

        public 测试结果(string 名称)
        {
            测试名称 = 名称;
            真实轨迹 = new List<轨迹点>();
            预测结果 = new List<预测结果点>();
            统计 = new 性能统计();
        }
    }

    public struct 预测结果点
    {
        public long 观测时间ms;
        public long 预测时间ms;
        public Vector3D 真实位置;
        public Vector3D 预测位置;
        public Vector3D 预测速度;
        public double 位置误差;
        public double 线性误差;
        public double 圆周误差;
        public double 组合误差;
        public double 线性权重;
        public double 圆周权重;
    }

    public class 性能统计
    {
        public double 平均位置误差;
        public double 最大位置误差;
        public double 平均速度误差;
        public int 样本数量;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== TargetTracker 模块测试程序 ===\n");

            var 所有结果 = new List<测试结果>();
            var 所有轨迹 = new List<List<轨迹点>>();

            try
            {
                // 测试1: 直线运动
                Console.WriteLine("测试1: 直线匀速运动");
                var 轨迹1 = 轨迹生成器.生成直线轨迹(
                    Vector3D.Zero,
                    new Vector3D(100, 50, 30),
                    6.0,
                    1
                );
                所有轨迹.Add(轨迹1);
                所有结果.Add(执行单个测试("直线匀速运动", 轨迹1));

                // 测试2: 二次曲线（匀加速）
                Console.WriteLine("\n测试2: 二次曲线（匀加速运动）");
                var 轨迹2 = 轨迹生成器.生成二次曲线轨迹(
                    Vector3D.Zero,
                    new Vector3D(50, 0, 0),
                    new Vector3D(0, 50, 0),
                    10.0,
                    1
                );
                所有轨迹.Add(轨迹2);
                所有结果.Add(执行单个测试("二次曲线匀加速", 轨迹2));

                // 测试3: 圆周运动
                Console.WriteLine("\n测试3: 圆周运动");
                var 轨迹3 = 轨迹生成器.生成圆周运动轨迹(
                    Vector3D.Zero,
                    250,
                    1,
                    new Vector3D(0, 0, 1),
                    new Vector3D(1, 0, 0),
                    10,
                    1
                );
                所有轨迹.Add(轨迹3);
                所有结果.Add(执行单个测试("圆周运动", 轨迹3));

                // 测试4: 螺旋运动
                Console.WriteLine("\n测试4: 螺旋运动");
                var 轨迹4 = 轨迹生成器.生成螺旋轨迹(
                    Vector3D.Zero,
                    300,
                    0.3,
                    new Vector3D(0, 0, 30),
                    new Vector3D(0, 0, 1),
                    10.0,
                    1
                );
                所有轨迹.Add(轨迹4);
                所有结果.Add(执行单个测试("螺旋运动", 轨迹4));

                // 测试5: 正弦运动
                Console.WriteLine("\n测试5: 正弦运动");
                var 轨迹5 = 轨迹生成器.生成正弦运动轨迹(
                    Vector3D.Zero,
                    new Vector3D(50, 0, 0),
                    100,
                    0.1,
                    new Vector3D(0, 1, 0),
                    10.0,
                    1
                );
                所有轨迹.Add(轨迹5);
                所有结果.Add(执行单个测试("正弦运动", 轨迹5));

                // 测试6: 组合运动（复用前面5个轨迹）
                Console.WriteLine("\n测试6: 组合运动（多模型拼接）");
                所有结果.Add(测试组合运动(所有轨迹));

                // 输出CSV文件
                Console.WriteLine("\n正在生成CSV文件...");
                导出所有结果到CSV(所有结果);

                Console.WriteLine("\n测试完成！按任意键退出...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n发生错误:");
                Console.WriteLine($"类型: {ex.GetType().Name}");
                Console.WriteLine($"消息: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"\n内部异常:");
                    Console.WriteLine($"类型: {ex.InnerException.GetType().Name}");
                    Console.WriteLine($"消息: {ex.InnerException.Message}");
                }
            }
            
            // // 仅在交互模式下等待按键（无参数运行时）
            // if (args.Length == 0 && !Console.IsInputRedirected)
            // {
            //     Console.WriteLine("按任意键退出...");
            //     Console.ReadKey();
            // }
        }

        /// <summary>
        /// 执行单个测试
        /// </summary>
        static 测试结果 执行单个测试(string 测试名称, List<轨迹点> 轨迹)
        {
            var 结果 = new 测试结果(测试名称);
            结果.真实轨迹 = 轨迹;
            执行预测测试(轨迹, 结果, 4000, 333);
            输出统计信息(结果);
            return 结果;
        }

        /// <summary>
        /// 测试组合运动（将前面5个测试的轨迹首尾拼接）
        /// </summary>
        static 测试结果 测试组合运动(List<List<轨迹点>> 所有轨迹)
        {
            var 结果 = new 测试结果("组合运动");
            
            // 拼接前面5个轨迹
            var 组合轨迹 = 拼接轨迹(所有轨迹.ToArray());
            
            结果.真实轨迹 = 组合轨迹;
            执行预测测试(组合轨迹, 结果, 4000, 333);
            
            输出统计信息(结果);
            return 结果;
        }

        /// <summary>
        /// 拼接多个轨迹（进行时间和位置平移）
        /// </summary>
        static List<轨迹点> 拼接轨迹(params List<轨迹点>[] 轨迹数组)
        {
            var 组合轨迹 = new List<轨迹点>();
            Vector3D 当前位置偏移 = Vector3D.Zero;
            long 当前时间偏移 = 0;
            
            foreach (var 轨迹段 in 轨迹数组)
            {
                if (轨迹段.Count == 0) continue;
                
                // 跳过第一个点（除非是第一段），避免重复点
                int 起始索引 = (组合轨迹.Count == 0) ? 0 : 1;
                
                for (int i = 起始索引; i < 轨迹段.Count; i++)
                {
                    var 原点 = 轨迹段[i];
                    组合轨迹.Add(new 轨迹点(
                        原点.位置 + 当前位置偏移,
                        原点.速度,
                        原点.加速度,
                        原点.时间戳ms + 当前时间偏移
                    ));
                }
                
                // 更新偏移量
                当前位置偏移 = 组合轨迹[组合轨迹.Count - 1].位置;
                当前时间偏移 = 组合轨迹[组合轨迹.Count - 1].时间戳ms;
            }
            
            return 组合轨迹;
        }

        /// <summary>
        /// 执行预测测试
        /// </summary>
        /// <param name="轨迹">完整的真实轨迹数据（1ms精度）</param>
        /// <param name="结果">测试结果对象</param>
        /// <param name="预测时长ms">向前预测的时间长度（毫秒）</param>
        /// <param name="采样间隔ms">模拟实际场景中获取真实数据的间隔（毫秒）</param>
        static void 执行预测测试(List<轨迹点> 轨迹, 测试结果 结果, long 预测时长ms, long 采样间隔ms)
        {
            var tracker = new TargetTracker(4);
            
            // 预热所需的采样次数（队列长度）
            int 预热采样次数 = 4;
            
            double 总位置误差 = 0;
            double 最大位置误差 = 0;
            int 有效样本数 = 0;
            int 采样计数 = 0;
            
            long 轨迹结束时间 = 轨迹[轨迹.Count - 1].时间戳ms;

            for (int i = 0; i < 轨迹.Count; i += (int)采样间隔ms)
            {
                var 当前点 = 轨迹[i];
                
                // 更新tracker（只在采样点更新）
                tracker.UpdateTarget(当前点.位置, 当前点.时间戳ms, false);
                采样计数++;

                // 预热期过后才进行预测测试
                if (采样计数 > 预热采样次数)
                {
                    // 计算预测的目标时间
                    long 预测目标时间 = 当前点.时间戳ms + 预测时长ms;
                    
                    // 如果预测时间超过了轨迹数据范围，停止测试
                    if (预测目标时间 > 轨迹结束时间)
                    {
                        break;
                    }
                    
                    // 预测未来位置（从当前采样点直接预测，不需要额外的时间补偿）
                    var 预测 = tracker.PredictFutureTargetInfo(预测时长ms, false);
                    
                    // 找到真实的未来位置
                    int 未来索引 = 查找时间戳索引(轨迹, 预测目标时间);
                    if (未来索引 >= 0 && 未来索引 < 轨迹.Count)
                    {
                        var 真实未来点 = 轨迹[未来索引];
                        double 位置误差 = (预测.Position - 真实未来点.位置).Length();
                        
                        总位置误差 += 位置误差;
                        最大位置误差 = Math.Max(最大位置误差, 位置误差);
                        有效样本数++;

                        结果.预测结果.Add(new 预测结果点
                        {
                            观测时间ms = 当前点.时间戳ms,
                            预测时间ms = 预测时长ms,
                            真实位置 = 真实未来点.位置,
                            预测位置 = 预测.Position,
                            预测速度 = 预测.Velocity,
                            位置误差 = 位置误差,
                            线性误差 = tracker.linearError,
                            圆周误差 = tracker.circularError,
                            组合误差 = tracker.combinationError,
                            线性权重 = tracker.linearWeight,
                            圆周权重 = tracker.circularWeight
                        });
                    }
                }
            }

            // 计算统计
            结果.统计.样本数量 = 有效样本数;
            结果.统计.平均位置误差 = 有效样本数 > 0 ? 总位置误差 / 有效样本数 : 0;
            结果.统计.最大位置误差 = 最大位置误差;
        }

        /// <summary>
        /// 使用二分查找找最接近指定时间戳的索引（O(log n)时间复杂度）
        /// </summary>
        static int 查找时间戳索引(List<轨迹点> 轨迹, long 目标时间戳)
        {
            if (轨迹.Count == 0)
                return -1;
            
            // 二分查找
            int left = 0, right = 轨迹.Count - 1;
            
            while (left < right)
            {
                int mid = left + (right - left) / 2;
                if (轨迹[mid].时间戳ms < 目标时间戳)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid;
                }
            }
            
            // 检查left和left-1，找出最接近的
            if (left >= 轨迹.Count)
                return 轨迹.Count - 1;
            
            if (left == 0)
                return 0;
            
            long diff_left = Math.Abs(轨迹[left].时间戳ms - 目标时间戳);
            long diff_prev = Math.Abs(轨迹[left - 1].时间戳ms - 目标时间戳);
            
            return diff_left < diff_prev ? left : left - 1;
        }

        /// <summary>
        /// 输出统计信息
        /// </summary>
        static void 输出统计信息(测试结果 结果)
        {
            Console.WriteLine($"  样本数量: {结果.统计.样本数量}");
            Console.WriteLine($"  平均位置误差: {结果.统计.平均位置误差:F2} 米");
            Console.WriteLine($"  最大位置误差: {结果.统计.最大位置误差:F2} 米");
        }

        /// <summary>
        /// 导出所有结果到CSV文件
        /// </summary>
        static void 导出所有结果到CSV(List<测试结果> 所有结果)
        {
            string 输出目录 = Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
            Directory.CreateDirectory(输出目录);

            foreach (var 结果 in 所有结果)
            {
                string 文件名 = Path.Combine(输出目录, $"{结果.测试名称}.csv");
                
                using (var writer = new StreamWriter(文件名, false, Encoding.UTF8))
                {
                    // 写入表头
                    writer.WriteLine("观测时间(ms),预测时长(ms),真实X,真实Y,真实Z,预测X,预测Y,预测Z,位置误差,线性误差,圆周误差,组合误差,线性权重,圆周权重");
                    
                    // 写入数据
                    foreach (var 点 in 结果.预测结果)
                    {
                        writer.WriteLine($"{点.观测时间ms},{点.预测时间ms}," +
                            $"{点.真实位置.X:F2},{点.真实位置.Y:F2},{点.真实位置.Z:F2}," +
                            $"{点.预测位置.X:F2},{点.预测位置.Y:F2},{点.预测位置.Z:F2}," +
                            $"{点.位置误差:F2},{点.线性误差:F2},{点.圆周误差:F2},{点.组合误差:F2}," +
                            $"{点.线性权重:F4},{点.圆周权重:F4}");
                    }
                }
                
                Console.WriteLine($"  已生成: {文件名}");
            }

            // 生成汇总统计文件
            string 汇总文件 = Path.Combine(输出目录, "汇总统计.csv");
            using (var writer = new StreamWriter(汇总文件, false, Encoding.UTF8))
            {
                writer.WriteLine("测试名称,样本数量,平均误差(m),最大误差(m)");
                foreach (var 结果 in 所有结果)
                {
                    writer.WriteLine($"{结果.测试名称},{结果.统计.样本数量}," +
                        $"{结果.统计.平均位置误差:F2},{结果.统计.最大位置误差:F2}");
                }
            }
            Console.WriteLine($"  已生成: {汇总文件}");
        }
    }
}
