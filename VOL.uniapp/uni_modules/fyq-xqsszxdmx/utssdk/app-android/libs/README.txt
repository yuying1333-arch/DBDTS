此目录不会被当前云打包依赖路径直接引用，仅作为备份目录。

文件名必须为：
SparkChain.aar

最终路径示例：
uni_modules/fyq-xqsszxdmx/libs/SparkChain.aar

说明：
1) 本插件已改为本地 AAR 依赖，不再从 Maven/JitPack 在线拉取。
2) 云打包读取的是 uni_modules/fyq-xqsszxdmx/libs/SparkChain.aar
3) 若文件名或路径不一致，会导致打包失败或 NoClassDefFoundError。
3) 替换 AAR 后请删除项目 unpackage 目录并重新运行到 Android 真机。
