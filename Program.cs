using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 火控系统状态枚举
    /// </summary>
    public enum 火控状态
    {
        初始化中,     // 系统初始化阶段
        待机,         // 无目标，等待中
        跟踪目标,     // 正在跟踪目标
        目标丢失      // 目标刚丢失，保持最后瞄准姿态
    }

    /// <summary>
    /// AI块火控炮塔系统 - 主程序
    /// 基于AI块获取目标坐标，自动控制网格上所有炮塔进行火控
    /// </summary>
    public partial class Program : MyGridProgram
    {
        #region 版本信息和常量
        private const string 版本号 = "1.0.0";
        private const string 系统名称 = "AI块火控炮塔系统";

        #endregion
        
        #region 核心组件
        /// <summary>参数管理器 - 管理所有可配置参数</summary>
        private 参数管理器 _参数管理器;

        /// <summary>目标跟踪器 - 负责目标位置预测和插值</summary>
        private TargetTracker _目标跟踪器;

        /// <summary>AI目标获取器 - 从AI块获取目标坐标</summary>
        private AI目标获取器 _目标获取器;

        /// <summary>炮塔管理器 - 管理炮塔的识别、分组和聚类</summary>
        private 炮塔管理器 _炮塔管理器;

        /// <summary>火控计算器 - 负责弹道计算和提前量预测</summary>
        private 火控计算器 _火控计算器;

        /// <summary>射击控制器 - 负责射击模式管理（齐射/轮射）</summary>
        private 射击控制器 _射击控制器;

        #endregion

        #region 控制器和状态

        /// <summary>主控制器（驾驶舱）- 用于获取舰船速度等信息</summary>
        private IMyShipController _主控制器;
        
        /// <summary>当前火控状态</summary>
        private 火控状态 _当前状态;

        /// <summary>帧计数器</summary>
        private int _帧计数;

        /// <summary>是否已完成初始化</summary>
        private bool _已初始化;

        /// <summary>上次目标更新帧</summary>
        private int _上次目标更新帧;

        /// <summary>当前目标位置（缓存）</summary>
        private Vector3D _当前目标位置;

        /// <summary>当前瞄准点（缓存）</summary>
        private Vector3D _当前瞄准点;

        /// <summary>聚类轮询调度 - 当前处理索引</summary>
        private int _当前聚类索引 = 0;

        /// <summary>聚类轮询调度 - 处理数量累加器（处理小数）</summary>
        private double _聚类处理累加器 = 0;

        #endregion

        #region 性能统计和日志

        private double _总运行时间ms;
        private double _最大运行时间ms;
        private int _运行次数;
        private StringBuilder _状态信息;
        private string _初始化错误信息;

        #endregion

        #region 构造函数

        /// <summary>
        /// 程序构造函数 - 初始化火控系统
        /// </summary>
        public Program()
        {
            // 初始化参数管理器（从CustomData读取配置）
            _参数管理器 = new 参数管理器(Me);

            // 初始化目标跟踪器
            _目标跟踪器 = new TargetTracker(_参数管理器.目标历史最大长度);

            // 初始化状态
            _当前状态 = 火控状态.初始化中;
            _帧计数 = 0;
            _已初始化 = false;
            _上次目标更新帧 = -9999;
            _当前目标位置 = Vector3D.Zero;
            _当前瞄准点 = Vector3D.Zero;

            // 初始化性能统计
            _总运行时间ms = 0;
            _最大运行时间ms = 0;
            _运行次数 = 0;
            _状态信息 = new StringBuilder();

            // 设置运行频率为每帧执行
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        #endregion

        #region 主循环

        /// <summary>
        /// 主循环入口
        /// </summary>
        public void Main(string argument, UpdateType updateSource)
        {
            _帧计数 = (_帧计数 + 1) % int.MaxValue;

            // 处理命令参数
            if (!string.IsNullOrEmpty(argument))
            {
                处理命令(argument);
            }

            // 初始化阶段
            if (!_已初始化)
            {
                执行初始化();
                return;
            }

            // 定期刷新方块列表
            if (_炮塔管理器.需要刷新(_帧计数))
            {
                _炮塔管理器.刷新炮塔列表(_帧计数);
                _射击控制器.初始化();
            }

            // 每帧获取目标位置（不能跳帧，否则会有延迟）
            Vector3D 目标位置 = _目标获取器.更新目标();
            bool 存在目标 = _目标获取器.存在有效目标;
            
            // 计算与上次目标更新的时间差（毫秒）
            long 时间差ms = MathHelper.帧数转毫秒(_帧计数 - _上次目标更新帧);
            // 检查目标位置是否更新（时间差>750ms时强制认为已更新）
            bool 目标位置需更新 = _目标获取器.目标位置需更新(目标位置, 时间差ms);

            // 更新状态机
            更新火控状态(存在目标, 目标位置, 目标位置需更新);     

            // 每帧执行火控主循环（轮询调度）
            执行火控循环();

            // 每帧处理射击需求队列（不跳帧，平滑处理）
            _射击控制器?.处理射击需求();

            // 固定10帧/次更新状态显示
            if (_帧计数 % 10 == 0)
            {
                显示状态信息();
            }

            // 更新性能统计
            更新性能统计();
            
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 执行初始化流程
        /// 分阶段初始化各组件
        /// </summary>
        private void 执行初始化()
        {
            // 阶段1：获取主控制器
            if (_主控制器 == null)
            {
                _主控制器 = 获取主控制器();
            }

            // 阶段2：初始化AI目标获取器
            if (_目标获取器 == null)
            {
                _目标获取器 = new AI目标获取器(GridTerminalSystem, _参数管理器);
                if (!_目标获取器.初始化())
                {
                    _初始化错误信息 = "AI目标获取器初始化失败";
                    显示初始化状态();
                    return;
                }
            }

            // 阶段3：初始化炮塔管理器
            if (_炮塔管理器 == null)
            {
                _炮塔管理器 = new 炮塔管理器(GridTerminalSystem, _参数管理器);
                if (!_炮塔管理器.刷新炮塔列表(_帧计数))
                {
                    _初始化错误信息 = "未找到可用炮塔";
                    显示初始化状态();
                    return;
                }
            }

            // 阶段4：初始化火控计算器
            if (_火控计算器 == null)
            {
                _火控计算器 = new 火控计算器(_参数管理器, _目标跟踪器);
            }

            // 阶段5：初始化射击控制器（内部会创建射击需求处理器）
            if (_射击控制器 == null)
            {
                _射击控制器 = new 射击控制器(_参数管理器, _炮塔管理器, _火控计算器);
                _射击控制器.初始化();
            }

            // 初始化完成
            _已初始化 = true;
            _当前状态 = 火控状态.待机;
            _初始化错误信息 = null;
        }

        /// <summary>
        /// 获取主控制器（驾驶舱）
        /// 优先查找带有主驾驶舱标签的控制器
        /// </summary>
        private IMyShipController 获取主控制器()
        {
            List<IMyShipController> 控制器列表 = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(控制器列表, c => c.IsFunctional && c.CanControlShip);

            if (控制器列表.Count == 0)
            {
                return null;
            }

            // 优先查找带有标签的控制器
            string 标签 = _参数管理器.主驾驶舱标签;
            for (int i = 0; i < 控制器列表.Count; i++)
            {
                if (控制器列表[i].CustomName.Contains(标签))
                {
                    return 控制器列表[i];
                }
            }

            // 没有找到带标签的，返回第一个
            return 控制器列表[0];
        }

        #endregion

        #region 聚类轮询调度

        /// <summary>
        /// 获取本帧应处理的聚类组列表
        /// 实现轮询调度，将火控计算均匀分布在多帧
        /// TODO：这个方法应该得放在炮塔管理器里更合适
        /// </summary>
        /// <returns>本帧需要处理的聚类组列表</returns>
        private List<炮塔聚类组> 获取本帧应处理的聚类组()
        {
            var 结果 = new List<炮塔聚类组>();
            
            // 直接获取缓存的聚类组列表（避免重复迭代）
            var 所有聚类 = _炮塔管理器.获取所有聚类组列表();

            int 聚类总数 = 所有聚类.Count;
            if (聚类总数 == 0) return 结果;

            // 计算每帧应处理的聚类数量（支持小数）
            // 例：10个聚类，周期20帧 -> 每帧10/20=0.5个 -> 每2帧处理1个
            // 例：30个聚类，周期20帧 -> 每帧30/20=1.5个 -> 每帧处理1-2个
            double 每帧处理数 = (double)聚类总数 / _参数管理器.火控更新周期;
            _聚类处理累加器 += 每帧处理数;

            // 取整数部分作为本帧处理数量
            int 本帧处理数量 = (int)_聚类处理累加器;
            _聚类处理累加器 -= 本帧处理数量;

            // 收集本帧应处理的聚类组
            for (int i = 0; i < 本帧处理数量; i++)
            {
                结果.Add(所有聚类[_当前聚类索引]);
                _当前聚类索引 = (_当前聚类索引 + 1) % 聚类总数;
            }

            return 结果;
        }

        #endregion

        #region 火控主循环

        /// <summary>
        /// 执行火控主循环
        /// 包含状态更新、火控计算、射击控制
        /// </summary>
        /// <param name="存在目标">是否存在有效目标</param>
        /// <param name="目标位置">目标位置</param>
        /// <param name="目标位置已更新">目标位置是否发生变化</param>
        private void 执行火控循环()
        {


            // 步骤2：根据状态执行相应逻辑
            switch (_当前状态)
            {
                case 火控状态.跟踪目标:
                    处理跟踪状态();
                    break;

                case 火控状态.待机:
                    处理待机状态();
                    break;

                case 火控状态.目标丢失:
                    处理目标丢失状态();
                    break;
            }
        }

        /// <summary>
        /// 更新火控状态机
        /// </summary>
        private void 更新火控状态(bool 存在目标, Vector3D 目标位置, bool 目标位置已更新)
        {
            switch (_当前状态)
            {
                case 火控状态.待机:
                    if (存在目标)
                    {
                        _当前状态 = 火控状态.跟踪目标;
                        _目标跟踪器.ClearHistory();
                        _炮塔管理器.使所有缓存失效();
                    }
                    break;

                case 火控状态.跟踪目标:
                    if (!存在目标)
                    {
                        _当前状态 = 火控状态.目标丢失;
                        _上次目标更新帧 = _帧计数;
                    }
                    else if (目标位置已更新)
                    {
                        _上次目标更新帧 = _帧计数;
                        long 当前时间戳ms = MathHelper.帧数转毫秒(_帧计数);
                        _目标跟踪器.UpdateTarget(目标位置, Vector3D.Zero, 当前时间戳ms);
                        _当前目标位置 = 目标位置;
                    }
                    break;

                case 火控状态.目标丢失:
                    if (存在目标)
                    {
                        _当前状态 = 火控状态.跟踪目标;
                    }
                    else
                    {
                        // 目标丢失后立即进入待机，不继续射击
                        _当前状态 = 火控状态.待机;
                        _射击控制器.重置();
                        _目标跟踪器.ClearHistory();
                    }
                    break;
            }
        }

        /// <summary>
        /// 处理待机状态
        /// </summary>
        private void 处理待机状态()
        {
            // 待机状态下不做任何操作
            // 炮塔保持最后姿态
        }

        /// <summary>
        /// 处理跟踪目标状态
        /// </summary>
        private void 处理跟踪状态()
        {
            // 获取舰船信息
            Vector3D 舰船速度 = Vector3D.Zero;
            Vector3D 重力向量 = Vector3D.Zero;
            if (_主控制器 != null)
            {
                舰船速度 = _主控制器.GetShipVelocities().LinearVelocity;
                重力向量 = _主控制器.GetNaturalGravity();
            }

            // if(_目标跟踪器.GetHistoryCount() < 1) return;// 暂时缓解目标跟踪器历史数据不足时乱打的问题// 已经通过将AI攻击块更新间隔设置为0解决
            
            // 获取本帧应处理的聚类组（轮询调度）
            var 本帧聚类组列表 = 获取本帧应处理的聚类组();

            // 对每个需要处理的聚类组执行火控计算
            foreach (var 聚类组 in 本帧聚类组列表)
            {
                // 计算时间偏移（从上次目标更新到现在）
                long 时间偏移ms = MathHelper.帧数转毫秒(_帧计数 - _上次目标更新帧);
                
                _射击控制器.对聚类组开启射击(聚类组, 舰船速度, 重力向量, 时间偏移ms);
                
                // 更新全局瞄准点缓存（供调试显示使用）
                if (聚类组.缓存有效)
                {
                    _当前瞄准点 = 聚类组.缓存瞄准点;
                }
            }
        }

        /// <summary>
        /// 处理目标丢失状态
        /// </summary>
        private void 处理目标丢失状态()
        {
            // 目标丢失后，停止射击，保持最后瞄准姿态
            // 状态机会立即转入待机状态
        }

        #endregion

        #region 命令处理

        /// <summary>
        /// 处理命令参数
        /// </summary>
        private void 处理命令(string 命令)
        {
            string 小写命令 = 命令.ToLower().Trim();

            switch (小写命令)
            {
                case "reset":
                case "重置":
                    重置系统();
                    break;

                case "refresh":
                case "刷新":
                    _炮塔管理器?.刷新炮塔列表(_帧计数);
                    _射击控制器?.初始化();
                    break;

                case "status":
                case "状态":
                    // 状态会在下一帧的显示状态信息中展示
                    break;
            }
        }

        /// <summary>
        /// 重置整个系统
        /// </summary>
        private void 重置系统()
        {
            _已初始化 = false;
            _当前状态 = 火控状态.初始化中;
            _目标获取器?.重置();
            _射击控制器?.重置();
            _目标跟踪器?.ClearHistory();
            _炮塔管理器?.使所有缓存失效();
            _初始化错误信息 = null;

            // 重置组件引用，下次主循环会重新初始化
            _目标获取器 = null;
            _炮塔管理器 = null;
            _火控计算器 = null;
            _射击控制器 = null;
            _主控制器 = null;

            // 重新加载参数
            _参数管理器 = new 参数管理器(Me);
        }

        #endregion

        #region 状态显示

        /// <summary>
        /// 显示初始化状态（初始化阶段调用）
        /// </summary>
        private void 显示初始化状态()
        {
            _状态信息.Clear();
            _状态信息.AppendLine($"[=== {系统名称} v{版本号} ===]");
            _状态信息.AppendLine("状态: 初始化中...");

            if (!string.IsNullOrEmpty(_初始化错误信息))
            {
                _状态信息.AppendLine($"错误: {_初始化错误信息}");
            }

            Echo(_状态信息.ToString());
        }

        /// <summary>
        /// 显示状态信息（每帧调用，持久化刷新）
        /// </summary>
        private void 显示状态信息()
        {
            _状态信息.Clear();
            _状态信息.AppendLine($"[=== {系统名称} v{版本号} ===]");

            if (!_已初始化)
            {
                _状态信息.AppendLine("[状态]: 初始化中...");
                if (!string.IsNullOrEmpty(_初始化错误信息))
                {
                    _状态信息.AppendLine($"错误: {_初始化错误信息}");
                }
                Echo(_状态信息.ToString());
                return;
            }

            // 基础状态
            _状态信息.AppendLine($"[状态]: {_当前状态} | 周期: {_参数管理器.火控更新周期}帧");
            _状态信息.AppendLine($"炮塔: {_炮塔管理器?.炮塔总数 ?? 0} | 聚类: {_炮塔管理器?.聚类组总数 ?? 0} | 火炮: {_炮塔管理器?.火炮类数量 ?? 0}");

            // 目标信息
            if (_目标获取器?.存在有效目标 == true)
            {
                double 距离 = (_当前目标位置 - (_主控制器?.GetPosition() ?? Vector3D.Zero)).Length();
                _状态信息.AppendLine($"目标距离: {距离:F0}m");
                _状态信息.AppendLine($"历史: {_目标跟踪器?.GetHistoryCount() ?? 0} | 误差: {_目标跟踪器?.combinationError:F2}m/s");
                _状态信息.AppendLine($"圆周：{_目标跟踪器.circularWeight:F2} | 线性：{_目标跟踪器.linearWeight:F2}");
                _状态信息.AppendLine($"圆周误差: {_目标跟踪器.circularError:F1}m/s | 线性误差: {_目标跟踪器.linearError:F1}m/s");
            }
            else
            {
                _状态信息.AppendLine("无目标");
            }

            // 性能统计（简化显示，单位为微秒）
            double 平均时间 = _运行次数 > 0 ? _总运行时间ms / _运行次数 : 0;
            _状态信息.AppendLine($"[性能]: {平均时间 * 1000:F0}us(avg)\n{_最大运行时间ms * 1000:F0}us(max)");
            _状态信息.AppendLine($"指令: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");

            Echo(_状态信息.ToString());
        }

        /// <summary>
        /// 更新性能统计
        /// </summary>
        private void 更新性能统计()
        {
            double 本次运行时间 = Runtime.LastRunTimeMs;
            _总运行时间ms += 本次运行时间;
            _运行次数++;

            if (本次运行时间 > _最大运行时间ms)
            {
                _最大运行时间ms = 本次运行时间;
            }

            // 定期重置统计
            if (_运行次数 >= _参数管理器.性能统计重置间隔)
            {
                _总运行时间ms = 本次运行时间;
                _最大运行时间ms = 本次运行时间;
                _运行次数 = 1;
            }
        }

        #endregion
    }
}
