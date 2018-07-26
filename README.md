# GPUAnimatorPlugin
有两种GPU加速模式<br>
1，每一帧缓存顶点坐标，顶点着色器根据帧数和顶点编号获取顶点进行渲染；<br>
2，每一帧缓存骨骼变换矩阵，顶点着色器计算蒙皮；<br>
# 综述
1，模式一，优点几乎不占用cpu和gpu消耗缺点动画文件体积较大（30帧左右，大概3M大小）；<br>
2，模式二，优点动画文件体积比原生动画文件还小，不占用CPU，GPU计算蒙皮；缺点GPU压力；<br>
# 部分算法参考
https://github.com/genechiu/GpuAnimation
