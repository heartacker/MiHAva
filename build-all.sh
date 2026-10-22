#!/usr/bin/env bash
set -e

echo "=============================================="
echo "    MiHAva Multi-Platform Build Pipeline      "
echo "=============================================="

# 1. 编译全平台解决方案
echo -e "\n[1/4] Building solution in Release mode..."
dotnet build MiHAva.slnx -c Release

# 2. 运行协议与状态层单元测试
echo -e "\n[2/4] Running automated unit tests..."
dotnet test tests/MiJia.Net.Tests/MiJia.Net.Tests.csproj -c Release --no-build

# 3. 打包 MiJia.Net NuGet 包
echo -e "\n[3/4] Packing MiJia.Net for NuGet..."
(cd src/MiJia.Net && dotnet pack -c Release --no-build)

# 4. 发布桌面端 Native AOT 单文件原生二进制
echo -e "\n[4/4] Publishing Desktop Native AOT single-file binary (.NET 11)..."
dotnet publish src/MiHAva.Desktop/MiHAva.Desktop.csproj -c Release

echo -e "\n=============================================="
echo "    [✓] All Pipeline Targets Succeeded!       "
echo "=============================================="
