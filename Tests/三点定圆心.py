import numpy as np

def get_circle_center_3d(a, b, c):
    # 三点坐标
    a = np.array(a, dtype=float)
    b = np.array(b, dtype=float)
    c = np.array(c, dtype=float)

    # 两个中垂线的法向量
    ab = b - a
    ac = c - a
    # 平面法向量
    n = np.cross(ab, ac)

    # 构造方程组
    mat = np.array([
        2 * (b - a),
        2 * (c - a),
        n
    ])
    rhs = np.array([
        np.dot(b, b) - np.dot(a, a),
        np.dot(c, c) - np.dot(a, a),
        np.dot(n, a)
    ])
    # 解方程
    center = np.linalg.solve(mat, rhs)
    return center

def main():
    print("请输入三点坐标，每行一个点，格式如: x,y,z")
    pts = []
    for i in range(3):
        s = input(f"第{i+1}个点: ")
        pts.append([float(x) for x in s.strip().split(',')])
    center = get_circle_center_3d(pts[0], pts[1], pts[2])
    print("圆心坐标为: {:.6f} {:.6f} {:.6f}".format(*center))

if __name__ == "__main__":
    main()