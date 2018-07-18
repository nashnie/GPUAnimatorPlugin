# GPUAnimatorPlugin
Optimization for animation systems.
The animation system has two performance hotspots, one is animation file volume optimization, such as compression, precision reduction, etc.; the second is skin optimization, such as pre-calculation, offline calculation of skin buffer vertex data, or skinning calculation using GPU acceleration; The former space changes time, and the latter GPU changes CPU. Both of these optimizations have problems with inconvenient animation mixing and require ongoing research.
# Partial algorithm reference
https://github.com/genechiu/GpuAnimation
