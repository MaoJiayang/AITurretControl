using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// AI目标获取器 - 负责从AI块获取目标信息
    /// 需要一个攻击AI块和一个飞行AI块配合使用
    /// </summary>
    public class AI目标获取器
    {
        #region 字段

        /// <summary>网格终端系统引用</summary>
        private IMyGridTerminalSystem _网格终端;

        /// <summary>参数管理器引用</summary>
        private 参数管理器 _参数;

        /// <summary>攻击AI块</summary>
        private IMyOffensiveCombatBlock _攻击块;

        /// <summary>飞行AI块</summary>
        private IMyFlightMovementBlock _飞行块;

        /// <summary>是否已初始化</summary>
        private bool _已初始化;

        /// <summary>上次目标位置</summary>
        private Vector3D _上次目标位置;

        /// <summary>上次目标ID</summary>
        private long _上次目标ID;

        /// <summary>调试输出委托</summary>
        private Action<string> _输出;

        #endregion

        #region 属性

        /// <summary>是否已初始化</summary>
        public bool 已初始化 => _已初始化;

        /// <summary>是否存在有效目标</summary>
        public bool 存在有效目标 { get; private set; }

        /// <summary>当前目标位置</summary>
        public Vector3D 目标位置 { get; private set; }

        /// <summary>当前目标ID（用于判断目标切换）</summary>
        public long 目标ID => _上次目标ID;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="网格终端">网格终端系统</param>
        /// <param name="参数">参数管理器</param>
        /// <param name="输出">调试输出委托（可选）</param>
        public AI目标获取器(IMyGridTerminalSystem 网格终端, 参数管理器 参数, Action<string> 输出 = null)
        {
            _网格终端 = 网格终端;
            _参数 = 参数;
            _输出 = 输出;

            _已初始化 = false;
            存在有效目标 = false;
            目标位置 = Vector3D.Zero;
            _上次目标位置 = Vector3D.NegativeInfinity;
            _上次目标ID = 0;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化AI块
        /// 搜索并配置攻击AI块和飞行AI块
        /// </summary>
        /// <returns>是否成功初始化</returns>
        public bool 初始化()
        {
            // 搜索攻击AI块
            List<IMyOffensiveCombatBlock> 攻击块列表 = new List<IMyOffensiveCombatBlock>();
            _网格终端.GetBlocksOfType(攻击块列表, b => b.IsFunctional);

            if (攻击块列表.Count == 0)
            {
                _输出?.Invoke("错误: 未找到攻击AI块");
                _已初始化 = false;
                return false;
            }
            _攻击块 = 攻击块列表[0];

            // 搜索飞行AI块
            List<IMyFlightMovementBlock> 飞行块列表 = new List<IMyFlightMovementBlock>();
            _网格终端.GetBlocksOfType(飞行块列表, b => b.IsFunctional);

            if (飞行块列表.Count == 0)
            {
                _输出?.Invoke("错误: 未找到飞行AI块");
                _已初始化 = false;
                return false;
            }
            _飞行块 = 飞行块列表[0];

            // 配置AI块
            配置AI块();

            _已初始化 = true;
            _输出?.Invoke("AI目标获取器初始化完成");
            return true;
        }

        /// <summary>
        /// 更新目标信息
        /// 从飞行AI块获取目标坐标
        /// </summary>
        /// <returns>目标位置，如果无目标返回Vector3D.NegativeInfinity</returns>
        public Vector3D 更新目标()
        {
            if (!_已初始化)
            {
                存在有效目标 = false;
                return Vector3D.NegativeInfinity;
            }

            // 从飞行块获取目标位置
            Vector3D 新目标位置 = 从飞行块获取目标();

            if (新目标位置.Equals(Vector3D.NegativeInfinity))
            {
                存在有效目标 = false;
                目标位置 = Vector3D.Zero;
                return Vector3D.NegativeInfinity;
            }

            // 检查目标是否改变（用于判断目标切换）
            bool 目标已改变 = !新目标位置.Equals(_上次目标位置);

            _上次目标位置 = 新目标位置;
            存在有效目标 = true;
            目标位置 = 新目标位置;

            return 新目标位置;
        }

        /// <summary>
        /// 检查目标位置是否已更新
        /// AI块的数据刷新率约3次/秒
        /// </summary>
        /// <param name="新位置">新的目标位置</param>
        /// <returns>位置是否变化</returns>
        public bool 目标位置已更新(Vector3D 新位置)
        {
            const double 最小位置变化 = 0.01; // 1厘米
            return (新位置 - _上次目标位置).LengthSquared() > 最小位置变化 * 最小位置变化;
        }

        /// <summary>
        /// 重置目标获取器状态
        /// </summary>
        public void 重置()
        {
            存在有效目标 = false;
            目标位置 = Vector3D.Zero;
            _上次目标位置 = Vector3D.NegativeInfinity;
            _上次目标ID = 0;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 配置AI块的工作状态
        /// 攻击块开启且启用AI，飞行块关闭但启用AI
        /// </summary>
        private void 配置AI块()
        {
            // 配置攻击AI块 - 开启并启用AI功能
            if (_攻击块 != null)
            {
                _攻击块.Enabled = true;
                _攻击块.ApplyAction("ActivateBehavior_On");
            }

            // 配置飞行AI块 - 关闭但启用AI功能（用于获取目标坐标）
            if (_飞行块 != null)
            {
                _飞行块.Enabled = false;
                _飞行块.ApplyAction("ActivateBehavior_On");
            }

            _输出?.Invoke("AI块配置完成");
        }

        /// <summary>
        /// 从飞行AI块获取目标坐标
        /// </summary>
        /// <returns>目标世界坐标，无目标时返回Vector3D.NegativeInfinity</returns>
        private Vector3D 从飞行块获取目标()
        {
            if (_飞行块 == null)
            {
                return Vector3D.NegativeInfinity;
            }

            // 获取飞行块的路径点列表
            List<IMyAutopilotWaypoint> 路径点列表 = new List<IMyAutopilotWaypoint>();
            _飞行块.GetWaypoints(路径点列表);

            if (路径点列表.Count == 0)
            {
                return Vector3D.NegativeInfinity;
            }

            // 获取最后一个路径点作为目标
            IMyAutopilotWaypoint 路径点 = 路径点列表[路径点列表.Count - 1];

            // 从世界矩阵中提取位置
            MatrixD 矩阵 = 路径点.Matrix;
            Vector3D 位置 = new Vector3D(矩阵.M41, 矩阵.M42, 矩阵.M43);

            return 位置;
        }

        #endregion
    }
}
