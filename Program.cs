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

        #endregion

        #region 性能统计

        private double _总运行时间ms;
        private double _最大运行时间ms;
        private int _运行次数;
        private StringBuilder _状态信息;

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

            Echo($"{系统名称} v{版本号} 启动中...");
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

            // 执行火控主循环（跳帧控制）
            if (_帧计数 % _参数管理器.火控更新跳帧 == 0)
            {
                执行火控循环();
            }

            // 更新显示和性能统计
            更新性能统计();
            显示状态信息();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 执行初始化流程
        /// 分阶段初始化各组件
        /// </summary>
        private void 执行初始化()
        {
            Echo("正在初始化火控系统...");

            // 阶段1：获取主控制器
            if (_主控制器 == null)
            {
                _主控制器 = 获取主控制器();
                if (_主控制器 == null)
                {
                    Echo("警告: 未找到驾驶舱，部分功能受限");
                }
                else
                {
                    Echo($"找到主控制器: {_主控制器.CustomName}");
                }
            }

            // 阶段2：初始化AI目标获取器
            if (_目标获取器 == null)
            {
                _目标获取器 = new AI目标获取器(GridTerminalSystem, _参数管理器, Echo);
                if (!_目标获取器.初始化())
                {
                    Echo("错误: AI目标获取器初始化失败");
                    return;
                }
            }

            // 阶段3：初始化炮塔管理器
            if (_炮塔管理器 == null)
            {
                _炮塔管理器 = new 炮塔管理器(GridTerminalSystem, _参数管理器, Echo);
                if (!_炮塔管理器.刷新炮塔列表(_帧计数))
                {
                    Echo("错误: 未找到可用炮塔");
                    return;
                }
            }

            // 阶段4：初始化火控计算器
            if (_火控计算器 == null)
            {
                _火控计算器 = new 火控计算器(_参数管理器, _目标跟踪器, Echo);
            }

            // 阶段5：初始化射击控制器
            if (_射击控制器 == null)
            {
                _射击控制器 = new 射击控制器(_参数管理器, _炮塔管理器, Echo);
                _射击控制器.初始化();
            }

            // 初始化完成
            _已初始化 = true;
            _当前状态 = 火控状态.待机;
            Echo("火控系统初始化完成！");
            Echo($"炮塔数量: {_炮塔管理器.炮塔总数}");
            Echo($"聚类组数: {_炮塔管理器.聚类组总数}");
            Echo($"火炮类数: {_炮塔管理器.火炮类数量}");
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

        #region 火控主循环

        /// <summary>
        /// 执行火控主循环
        /// 包含目标获取、状态更新、火控计算、射击控制
        /// </summary>
        private void 执行火控循环()
        {
            // 步骤1：获取目标
            Vector3D 目标位置 = _目标获取器.更新目标();
            bool 存在目标 = _目标获取器.存在有效目标;

            // 步骤2：更新状态机
            更新火控状态(存在目标, 目标位置);

            // 步骤3：根据状态执行相应逻辑
            switch (_当前状态)
            {
                case 火控状态.待机:
                    处理待机状态();
                    break;

                case 火控状态.跟踪目标:
                    处理跟踪状态(目标位置);
                    break;

                case 火控状态.目标丢失:
                    处理目标丢失状态();
                    break;
            }
        }

        /// <summary>
        /// 更新火控状态机
        /// </summary>
        private void 更新火控状态(bool 存在目标, Vector3D 目标位置)
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
                    else if (_目标获取器.目标位置已更新(目标位置))
                    {
                        _上次目标更新帧 = _帧计数;
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
        private void 处理跟踪状态(Vector3D 目标位置)
        {
            // 更新目标跟踪器
            long 当前时间戳ms = (long)(_帧计数 * _参数管理器.时间常数 * 1000);
            _目标跟踪器.UpdateTarget(目标位置, Vector3D.Zero, 当前时间戳ms);
            _当前目标位置 = 目标位置;

            // 获取舰船信息
            Vector3D 舰船速度 = Vector3D.Zero;
            Vector3D 重力向量 = Vector3D.Zero;
            if (_主控制器 != null)
            {
                舰船速度 = _主控制器.GetShipVelocities().LinearVelocity;
                重力向量 = _主控制器.GetNaturalGravity();
            }

            // 对每个聚类组执行火控计算
            执行聚类组火控计算(舰船速度, 重力向量);

            // 执行射击控制
            _射击控制器.执行射击控制(_帧计数, _当前瞄准点, true);
        }

        /// <summary>
        /// 处理目标丢失状态
        /// </summary>
        private void 处理目标丢失状态()
        {
            // 目标丢失后，停止射击，保持最后瞄准姿态
            // 状态机会立即转入待机状态
        }

        /// <summary>
        /// 对所有聚类组执行火控计算
        /// </summary>
        private void 执行聚类组火控计算(Vector3D 舰船速度, Vector3D 重力向量)
        {
            // TODO: 实现聚类组火控计算
            // 遍历所有聚类组
            // 对每个组的代表炮塔计算瞄准点
            // 缓存计算结果供组内其他炮塔使用

            foreach (var 聚类组 in _炮塔管理器.获取所有聚类组())
            {
                // 获取代表炮塔
                var 代表 = 聚类组.代表炮塔;
                if (!代表.是否可用())
                    continue;

                // 获取炮塔信息
                Vector3D 炮塔位置 = 代表.获取位置();
                double 弹速 = 代表.静态信息.弹速;
                bool 受重力影响 = 代表.静态信息.受重力影响;

                // 计算瞄准点
                // TODO: 调用火控计算器计算瞄准点
                // Vector3D 瞄准点 = _火控计算器.计算瞄准点(炮塔位置, 弹速, 受重力影响, 舰船速度, 重力向量);

                // 暂时使用目标位置作为瞄准点（待实现完整计算）
                Vector3D 瞄准点 = _当前目标位置;

                // 缓存结果
                聚类组.缓存瞄准点 = 瞄准点;
                聚类组.缓存有效 = true;
                聚类组.上次计算帧 = _帧计数;

                // 更新全局瞄准点缓存
                _当前瞄准点 = 瞄准点;
            }
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
                    Echo("已刷新炮塔列表");
                    break;

                case "toggle":
                case "切换":
                    _参数管理器.火炮类使用轮射 = !_参数管理器.火炮类使用轮射;
                    Echo($"火炮射击模式: {(_参数管理器.火炮类使用轮射 ? "轮射" : "齐射")}");
                    break;

                case "status":
                case "状态":
                    显示详细状态();
                    break;

                default:
                    Echo($"未知命令: {命令}");
                    Echo("可用命令: 重置, 刷新, 切换, 状态");
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

            // 重新加载参数
            _参数管理器 = new 参数管理器(Me);

            Echo("系统已重置");
        }

        #endregion

        #region 状态显示

        /// <summary>
        /// 显示状态信息
        /// </summary>
        private void 显示状态信息()
        {
            _状态信息.Clear();
            _状态信息.AppendLine($"=== {系统名称} v{版本号} ===");
            _状态信息.AppendLine($"状态: {_当前状态}");

            if (_已初始化)
            {
                _状态信息.AppendLine($"炮塔: {_炮塔管理器?.炮塔总数 ?? 0}个");
                _状态信息.AppendLine($"聚类组: {_炮塔管理器?.聚类组总数 ?? 0}个");
                _状态信息.AppendLine($"火炮类: {_炮塔管理器?.火炮类数量 ?? 0}门");
                _状态信息.AppendLine($"射击模式: {(_参数管理器.火炮类使用轮射 ? "轮射" : "齐射")}");

                if (_目标获取器?.存在有效目标 == true)
                {
                    double 距离 = (_当前目标位置 - (_主控制器?.GetPosition() ?? Vector3D.Zero)).Length();
                    _状态信息.AppendLine($"目标距离: {距离:F0}m");
                    _状态信息.AppendLine($"跟踪历史: {_目标跟踪器?.GetHistoryCount() ?? 0}");
                    _状态信息.AppendLine($"预测误差: {_目标跟踪器?.combinationError:F2}m/s");
                }
                else
                {
                    _状态信息.AppendLine("无目标");
                }
            }

            // 性能统计
            _状态信息.AppendLine("--- 性能 ---");
            _状态信息.AppendLine($"平均: {(_运行次数 > 0 ? _总运行时间ms / _运行次数 : 0):F3}ms");
            _状态信息.AppendLine($"最大: {_最大运行时间ms:F3}ms");
            _状态信息.AppendLine($"指令: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");

            Echo(_状态信息.ToString());
        }

        /// <summary>
        /// 显示详细状态（命令触发）
        /// </summary>
        private void 显示详细状态()
        {
            Echo("=== 详细状态 ===");
            Echo($"帧计数: {_帧计数}");
            Echo($"更新跳帧: {_参数管理器.火控更新跳帧}");
            Echo($"聚类距离: {_参数管理器.聚类距离}m");
            Echo($"迭代次数: {_参数管理器.弹道迭代次数}");

            if (_炮塔管理器 != null)
            {
                Echo("--- 炮塔分布 ---");
                foreach (var 聚类组 in _炮塔管理器.获取所有聚类组())
                {
                    Echo($"{聚类组.分组键}: {聚类组.炮塔列表.Count}门");
                }
            }
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
