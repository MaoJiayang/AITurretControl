using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// 射击需求 - 表示一个炮塔的射击状态需求
    /// </summary>
    public class 射击需求
    {
        /// <summary>目标炮塔</summary>
        public IMyLargeTurretBase 炮塔 { get; set; }

        /// <summary>需要设置的Shoot值</summary>
        public bool 需要射击 { get; set; }

        /// <summary>链表节点引用（用于O(1)查找和移除）</summary>
        public LinkedListNode<射击需求> 链表节点 { get; set; }

        public 射击需求(IMyLargeTurretBase 炮塔, bool 需要射击)
        {
            this.炮塔 = 炮塔;
            this.需要射击 = 需要射击;
            this.链表节点 = null;
        }
    }

    /// <summary>
    /// 射击需求处理器 - 使用哈希链表结构管理炮塔射击需求
    /// 
    /// 设计思路：
    /// 1. 使用Dictionary实现O(1)的炮塔查找
    /// 2. 使用LinkedList维护处理顺序（FIFO队列）
    /// 3. 每帧只处理有限数量的需求变更，避免性能尖峰
    /// 4. 需求变更时直接修改已存在的需求，无需重新入队
    /// </summary>
    public class 射击需求处理器
    {
        #region 字段

        /// <summary>参数管理器引用</summary>
        private 参数管理器 _参数;

        /// <summary>哈希表 - 用于O(1)查找炮塔对应的需求</summary>
        private Dictionary<IMyLargeTurretBase, 射击需求> _需求字典;

        /// <summary>链表 - 维护处理顺序（FIFO队列）</summary>
        private LinkedList<射击需求> _处理队列;

        /// <summary>已开火集合 - 记录所有Shoot=true的炮塔</summary>
        private HashSet<IMyLargeTurretBase> _已开火集合;

        /// <summary>累积计数器 - 用于支持小数参数的处理数量</summary>
        private double _累积计数器;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="参数">参数管理器</param>
        public 射击需求处理器(参数管理器 参数)
        {
            _参数 = 参数;
            _需求字典 = new Dictionary<IMyLargeTurretBase, 射击需求>();
            _处理队列 = new LinkedList<射击需求>();
            _已开火集合 = new HashSet<IMyLargeTurretBase>();
            _累积计数器 = 0.0;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 提交射击需求
        /// 如果炮塔已存在需求，直接修改；否则创建新需求入队
        /// </summary>
        /// <param name="炮塔">目标炮塔</param>
        /// <param name="需要射击">是否需要射击</param>
        public void 提交需求(IMyLargeTurretBase 炮塔, bool 需要射击)
        {
            if (炮塔 == null)
                return;

            射击需求 需求;
            if (_需求字典.TryGetValue(炮塔, out 需求))
            {
                // 已存在需求，直接更新
                需求.需要射击 = 需要射击;
            }
            else
            {
                // 创建新需求并加入队列
                需求 = new 射击需求(炮塔, 需要射击);
                需求.链表节点 = _处理队列.AddLast(需求);
                _需求字典[炮塔] = 需求;
            }
        }

        /// <summary>
        /// 处理队列中的需求
        /// 每次调用最多处理指定数量的变更
        /// 支持小数参数（通过累积计数器）
        /// </summary>
        /// <returns>本次实际处理的变更数量</returns>
        public int 处理需求()
        {
            // 累加本次应处理的数量
            _累积计数器 += _参数.每帧最大射击处理数;
            
            // 本次实际处理数为累积值的整数部分
            int 最大处理数 = (int)_累积计数器;
            int 已处理数 = 0;

            // 从队首开始遍历
            var 当前节点 = _处理队列.First;
            while (当前节点 != null && 已处理数 < 最大处理数)
            {
                var 需求 = 当前节点.Value;
                var 下一节点 = 当前节点.Next;

                // 检查炮塔是否有效
                if (需求.炮塔 == null || !需求.炮塔.IsFunctional)
                {
                    // 炮塔无效，移除需求
                    移除需求(需求);
                }
                else
                {
                    // 通过已开火集合判断是否需要实际操作
                    bool 当前在集合中 = _已开火集合.Contains(需求.炮塔);
                    bool 需要变更 = false;

                    if (需求.需要射击)
                    {
                        // 需求为开火，只有不在集合中才需要处理
                        if (!当前在集合中)
                        {
                            需求.炮塔.Shoot = true;
                            _已开火集合.Add(需求.炮塔);
                            需要变更 = true;
                        }
                    }
                    else
                    {
                        // 需求为停火，只有在集合中才需要处理
                        if (当前在集合中)
                        {
                            需求.炮塔.Shoot = false;
                            _已开火集合.Remove(需求.炮塔);
                            需要变更 = true;
                        }
                    }
                    
                    // 只有实际发生变更才计数
                    if (需要变更)
                    {
                        已处理数++;
                    }
                }

                当前节点 = 下一节点;
            }

            // 扣除已处理的数量，保留小数部分
            _累积计数器 -= 已处理数;

            return 已处理数;
        }

        /// <summary>
        /// 移除指定炮塔的需求
        /// </summary>
        /// <param name="炮塔">目标炮塔</param>
        public void 移除炮塔需求(IMyLargeTurretBase 炮塔)
        {
            if (炮塔 == null)
                return;

            射击需求 需求;
            if (_需求字典.TryGetValue(炮塔, out 需求))
            {
                移除需求(需求);
            }
        }

        /// <summary>
        /// 清空所有需求
        /// </summary>
        public void 清空()
        {
            _需求字典.Clear();
            _处理队列.Clear();
        }

        /// <summary>
        /// 强制停止所有射击
        /// 立即将所有已开火炮塔的Shoot设为false，不受每帧处理数限制
        /// </summary>
        public void 强制停止所有射击()
        {
            // 使用已开火集合快速停止所有射击
            foreach (var 炮塔 in _已开火集合)
            {
                if (炮塔 != null)
                {
                    炮塔.Shoot = false;
                }
            }
            _已开火集合.Clear();
            
            // 清空所有需求
            清空();
        }

        /// <summary>
        /// 重置处理器
        /// 清空所有需求并停止射击
        /// </summary>
        public void 重置()
        {
            强制停止所有射击();
            清空();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 移除需求
        /// </summary>
        /// <param name="需求">需要移除的需求</param>
        private void 移除需求(射击需求 需求)
        {
            if (需求 == null)
                return;

            // 从链表移除
            if (需求.链表节点 != null)
            {
                _处理队列.Remove(需求.链表节点);
                需求.链表节点 = null;
            }

            // 从字典移除
            _需求字典.Remove(需求.炮塔);
        }

        #endregion
    }
}
