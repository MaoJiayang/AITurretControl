import pandas as pd
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D
import numpy as np
import os
from matplotlib import rcParams

# 设置中文字体
rcParams['font.sans-serif'] = ['Microsoft YaHei', 'SimHei']
rcParams['axes.unicode_minus'] = False

# 测试结果目录
结果目录 = 'TestResults'

# 测试文件列表
测试文件 = [
    '直线匀速运动.csv',
    '二次曲线匀加速.csv',
    '圆周运动.csv',
    '螺旋运动.csv',
    '正弦运动.csv',
    '组合运动.csv'
]

def 读取数据(文件名):
    """读取CSV文件"""
    文件路径 = os.path.join(结果目录, 文件名)
    if not os.path.exists(文件路径):
        print(f"警告: 文件 {文件路径} 不存在")
        return None
    return pd.read_csv(文件路径)

def 绘制位置误差(ax, df, 标题):
    """绘制位置误差随时间变化"""
    ax.plot(df['观测时间(ms)'] / 1000, df['位置误差'], 'b-', linewidth=2, label='位置误差')
    ax.set_xlabel('时间 (秒)', fontsize=12)
    ax.set_ylabel('误差 (米)', fontsize=12)
    ax.set_title(f'{标题} - 位置误差', fontsize=14, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(fontsize=10)
    
    # 添加统计信息
    平均误差 = df['位置误差'].mean()
    最大误差 = df['位置误差'].max()
    ax.text(0.02, 0.98, f'平均: {平均误差:.2f}m\n最大: {最大误差:.2f}m',
            transform=ax.transAxes, verticalalignment='top',
            bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.5),
            fontsize=10)

def 绘制误差对比(ax, df, 标题):
    """绘制线性、圆周、组合误差对比"""
    时间 = df['观测时间(ms)'] / 1000
    ax.plot(时间, df['线性误差'], 'r-', linewidth=1.5, label='线性误差', alpha=0.7)
    ax.plot(时间, df['圆周误差'], 'g-', linewidth=1.5, label='圆周误差', alpha=0.7)
    ax.plot(时间, df['组合误差'], 'b-', linewidth=2, label='组合误差')
    ax.set_xlabel('时间 (秒)', fontsize=12)
    ax.set_ylabel('误差 (米/秒^2)', fontsize=12)
    ax.set_title(f'{标题} - 预测误差对比', fontsize=14, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(fontsize=10, loc='best')

def 绘制权重变化(ax, df, 标题):
    """绘制线性和圆周权重变化"""
    时间 = df['观测时间(ms)'] / 1000
    ax.plot(时间, df['线性权重'], 'r-', linewidth=2, label='线性权重')
    ax.plot(时间, df['圆周权重'], 'g-', linewidth=2, label='圆周权重')
    ax.set_xlabel('时间 (秒)', fontsize=12)
    ax.set_ylabel('权重', fontsize=12)
    ax.set_title(f'{标题} - 权重自适应变化', fontsize=14, fontweight='bold')
    ax.set_ylim(-0.05, 1.05)
    ax.grid(True, alpha=0.3)
    ax.legend(fontsize=10, loc='best')

def 绘制圆周半径变化(ax, df, 标题):
    """绘制圆周半径随时间的变化"""
    if '圆周半径' not in df.columns or '圆周有效' not in df.columns:
        ax.text(0.5, 0.5, '无圆周数据', ha='center', va='center', 
                transform=ax.transAxes, fontsize=14)
        return
    
    df_有效 = df[df['圆周有效'] == 1]
    if len(df_有效) == 0:
        ax.text(0.5, 0.5, '无有效圆周运动', ha='center', va='center',
                transform=ax.transAxes, fontsize=14)
        return
    
    时间 = df_有效['观测时间(ms)'] / 1000
    ax.plot(时间, df_有效['圆周半径'], 'purple', linewidth=2, label='圆周半径')
    ax.set_xlabel('时间 (秒)', fontsize=12)
    ax.set_ylabel('半径 (米)', fontsize=12)
    ax.set_title(f'{标题} - 圆周半径变化', fontsize=14, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(fontsize=10)
    
    # 添加统计信息
    平均半径 = df_有效['圆周半径'].mean()
    最大半径 = df_有效['圆周半径'].max()
    最小半径 = df_有效['圆周半径'].min()
    ax.text(0.02, 0.98, f'平均: {平均半径:.2f}m\n最大: {最大半径:.2f}m\n最小: {最小半径:.2f}m',
            transform=ax.transAxes, verticalalignment='top',
            bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.5),
            fontsize=10)

def 绘制圆心轨迹2D(ax, df, 标题):
    """绘制圆心轨迹的XY平面投影"""
    if '圆周有效' not in df.columns:
        ax.text(0.5, 0.5, '无圆周数据', ha='center', va='center',
                transform=ax.transAxes, fontsize=14)
        return
    
    df_有效 = df[df['圆周有效'] == 1]
    if len(df_有效) == 0:
        ax.text(0.5, 0.5, '无有效圆周运动', ha='center', va='center',
                transform=ax.transAxes, fontsize=14)
        return
    
    # 采样以避免过于密集
    采样间隔 = max(1, len(df_有效) // 200)
    df_采样 = df_有效.iloc[::采样间隔]
    
    # 绘制圆心轨迹
    ax.plot(df_采样['圆心X'], df_采样['圆心Y'], 'g-', linewidth=2, 
            label='圆心轨迹', alpha=0.7)
    
    # 绘制真实轨迹作为对比
    df_真实采样 = df.iloc[::采样间隔]
    ax.plot(df_真实采样['真实X'], df_真实采样['真实Y'], 'b--', 
            linewidth=1, label='真实轨迹', alpha=0.5)
    
    # 标记起点和终点
    ax.scatter(df_采样['圆心X'].iloc[0], df_采样['圆心Y'].iloc[0],
              c='lime', s=100, marker='^', label='圆心起点', zorder=5)
    ax.scatter(df_采样['圆心X'].iloc[-1], df_采样['圆心Y'].iloc[-1],
              c='darkgreen', s=100, marker='v', label='圆心终点', zorder=5)
    
    ax.set_xlabel('X (米)', fontsize=12)
    ax.set_ylabel('Y (米)', fontsize=12)
    ax.set_title(f'{标题} - 圆心轨迹 (XY投影)', fontsize=14, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(fontsize=10)
    ax.axis('equal')

def 绘制3D轨迹(ax, df, 标题):
    """绘制3D轨迹对比"""
    # 采样数据点（避免太密集）
    采样间隔 = max(1, len(df) // 100)
    df_采样 = df.iloc[::采样间隔]
    
    ax.plot(df_采样['真实X'], df_采样['真实Y'], df_采样['真实Z'], 
            'b-', linewidth=2, label='真实轨迹', alpha=0.8)
    ax.plot(df_采样['预测X'], df_采样['预测Y'], df_采样['预测Z'], 
            'r--', linewidth=1.5, label='预测轨迹', alpha=0.8)
    
    # 绘制圆心轨迹（仅当圆周有效时）
    if '圆周有效' in df.columns:
        df_有效圆周 = df[df['圆周有效'] == 1].iloc[::采样间隔]
        if len(df_有效圆周) > 0:
            ax.plot(df_有效圆周['圆心X'], df_有效圆周['圆心Y'], df_有效圆周['圆心Z'], 
                    'g:', linewidth=1.5, label='圆心轨迹', alpha=0.6)
            # 绘制第一个和最后一个有效圆心
            ax.scatter(df_有效圆周['圆心X'].iloc[0], df_有效圆周['圆心Y'].iloc[0], df_有效圆周['圆心Z'].iloc[0], 
                      c='lime', s=50, marker='^', alpha=0.7)
    
    # 绘制起点和终点
    ax.scatter(df['真实X'].iloc[0], df['真实Y'].iloc[0], df['真实Z'].iloc[0], 
              c='green', s=100, marker='o', label='起点')
    ax.scatter(df['真实X'].iloc[-1], df['真实Y'].iloc[-1], df['真实Z'].iloc[-1], 
              c='red', s=100, marker='s', label='终点')
    
    ax.set_xlabel('X (米)', fontsize=10)
    ax.set_ylabel('Y (米)', fontsize=10)
    ax.set_zlabel('Z (米)', fontsize=10)
    ax.set_title(f'{标题} - 3D轨迹对比', fontsize=12, fontweight='bold')
    ax.legend(fontsize=9)

def 绘制单个测试完整分析(文件名):
    """为单个测试生成完整分析图"""
    测试名 = 文件名.replace('.csv', '')
    df = 读取数据(文件名)
    if df is None:
        return
    
    # 创建2x3子图布局
    fig = plt.figure(figsize=(20, 12))
    
    # 1. 位置误差
    ax1 = plt.subplot(2, 3, 1)
    绘制位置误差(ax1, df, 测试名)
    
    # 2. 误差对比
    ax2 = plt.subplot(2, 3, 2)
    绘制误差对比(ax2, df, 测试名)
    
    # 3. 权重变化
    ax3 = plt.subplot(2, 3, 3)
    绘制权重变化(ax3, df, 测试名)
    
    # 4. 3D轨迹（包含圆心）
    ax4 = plt.subplot(2, 3, 4, projection='3d')
    绘制3D轨迹(ax4, df, 测试名)
    
    # 5. 圆周半径变化
    ax5 = plt.subplot(2, 3, 5)
    绘制圆周半径变化(ax5, df, 测试名)
    
    # 6. 预留位置（可以添加其他分析）
    ax6 = plt.subplot(2, 3, 6)
    绘制圆心轨迹2D(ax6, df, 测试名)
    
    plt.tight_layout()
    输出文件 = os.path.join(结果目录, f'{测试名}_分析.png')
    plt.savefig(输出文件, dpi=150, bbox_inches='tight')
    print(f'已保存: {输出文件}')
    plt.close()

def 绘制汇总对比():
    """绘制所有测试的汇总对比"""
    # 调整为3x2布局以容纳6个测试
    fig, axes = plt.subplots(2, 3, figsize=(20, 10))
    fig.suptitle('所有测试汇总对比', fontsize=16, fontweight='bold')
    
    # 将axes转为一维数组方便索引
    axes_flat = axes.flatten()
    
    for i, 文件名 in enumerate(测试文件):
        df = 读取数据(文件名)
        if df is None:
            continue
        
        测试名 = 文件名.replace('.csv', '')
        ax = axes_flat[i]
        
        时间 = df['观测时间(ms)'] / 1000
        ax.plot(时间, df['位置误差'], linewidth=2, label='位置误差')
        ax.plot(时间, df['组合误差'], linewidth=1.5, alpha=0.7, label='组合误差')
        
        ax.set_xlabel('时间 (秒)', fontsize=11)
        ax.set_ylabel('误差 (米)', fontsize=11)
        ax.set_title(测试名, fontsize=13, fontweight='bold')
        ax.grid(True, alpha=0.3)
        ax.legend(fontsize=9)
        
        # 添加统计信息
        平均误差 = df['位置误差'].mean()
        最大误差 = df['位置误差'].max()
        ax.text(0.98, 0.98, f'平均: {平均误差:.2f}m\n最大: {最大误差:.2f}m',
                transform=ax.transAxes, verticalalignment='top', horizontalalignment='right',
                bbox=dict(boxstyle='round', facecolor='lightblue', alpha=0.5),
                fontsize=9)
    
    plt.tight_layout()
    输出文件 = os.path.join(结果目录, '汇总对比.png')
    plt.savefig(输出文件, dpi=150, bbox_inches='tight')
    print(f'已保存: {输出文件}')
    plt.close()

def 绘制统计柱状图():
    """绘制各测试的统计柱状图"""
    测试名列表 = []
    平均误差列表 = []
    最大误差列表 = []
    
    for 文件名 in 测试文件:
        df = 读取数据(文件名)
        if df is None:
            continue
        测试名列表.append(文件名.replace('.csv', ''))
        平均误差列表.append(df['位置误差'].mean())
        最大误差列表.append(df['位置误差'].max())
    
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(14, 6))
    fig.suptitle('预测性能统计对比', fontsize=16, fontweight='bold')
    
    x = np.arange(len(测试名列表))
    width = 0.35
    
    # 平均误差
    bars1 = ax1.bar(x, 平均误差列表, width, color='steelblue', alpha=0.8)
    ax1.set_xlabel('测试类型', fontsize=12)
    ax1.set_ylabel('平均误差 (米)', fontsize=12)
    ax1.set_title('平均位置误差对比', fontsize=13)
    ax1.set_xticks(x)
    ax1.set_xticklabels(测试名列表, rotation=15, ha='right')
    ax1.grid(axis='y', alpha=0.3)
    
    # 在柱子上标注数值
    for bar in bars1:
        height = bar.get_height()
        ax1.text(bar.get_x() + bar.get_width()/2., height,
                f'{height:.2f}m', ha='center', va='bottom', fontsize=10)
    
    # 最大误差
    bars2 = ax2.bar(x, 最大误差列表, width, color='coral', alpha=0.8)
    ax2.set_xlabel('测试类型', fontsize=12)
    ax2.set_ylabel('最大误差 (米)', fontsize=12)
    ax2.set_title('最大位置误差对比', fontsize=13)
    ax2.set_xticks(x)
    ax2.set_xticklabels(测试名列表, rotation=15, ha='right')
    ax2.grid(axis='y', alpha=0.3)
    
    for bar in bars2:
        height = bar.get_height()
        ax2.text(bar.get_x() + bar.get_width()/2., height,
                f'{height:.2f}m', ha='center', va='bottom', fontsize=10)
    
    plt.tight_layout()
    输出文件 = os.path.join(结果目录, '统计对比.png')
    plt.savefig(输出文件, dpi=150, bbox_inches='tight')
    print(f'已保存: {输出文件}')
    plt.close()

def main():
    print("=" * 60)
    print("TargetTracker 测试结果可视化")
    print("=" * 60)
    
    if not os.path.exists(结果目录):
        print(f"错误: 找不到结果目录 {结果目录}")
        return
    
    # 为每个测试生成详细分析图
    print("\n生成各测试详细分析图...")
    for 文件名 in 测试文件:
        绘制单个测试完整分析(文件名)
    
    # 生成汇总对比图
    print("\n生成汇总对比图...")
    绘制汇总对比()
    
    # 生成统计柱状图
    print("\n生成统计柱状图...")
    绘制统计柱状图()
    
    print("\n" + "=" * 60)
    print("所有图表生成完成！")
    print(f"请查看 {结果目录} 目录")
    print("=" * 60)

if __name__ == '__main__':
    main()
