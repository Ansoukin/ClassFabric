{
  dotnetCorePackages,
  buildDotnetModule,
  libx11,
  libice,
  libsm,
  libxfixes,
  fontconfig,
  git,
  stdenv,
  autoPatchelfHook,
  makeDesktopItem,
}:
let
  desktopItem = makeDesktopItem {
    type = "Application";
    name = "cn.classfabric.app";
    desktopName = "ClassFabric";
    icon = "cn.classfabric.app";
    exec = "classfabric";
    terminal = false;
    startupNotify = true;
    comment = "功能强大、可定制、跨平台的大屏课表显示工具。";
    categories = [
      "Education"
      "Office"
    ];
  };
in
buildDotnetModule {
  pname = "classfabric";
  version = "2.1.1.0";
  projectFile = "./ClassFabric.Desktop/ClassFabric.Desktop.csproj";
  dotnet-sdk =
    with dotnetCorePackages;
    (combinePackages [
      sdk_9_0
      sdk_8_0
      sdk_6_0
    ]);
  dotnet-runtime = dotnetCorePackages.runtime_8_0;
  src = ../../.;
  # nix build .#classfabric.passthru.fetch-deps && ./result ./tools/nix/deps.json
  # 生成完后可能需要手动修改
  nugetDeps = ./deps.json;
  doCheck = true;
  dotnetBuildFlags = [
    "--property:NIX=true"
  ];
  runtimeDeps = [
    libx11
    libice
    libsm
    libxfixes
    fontconfig
  ];
  executables = [ "ClassFabric.Desktop" ];
  nativeBuildInputs = [
    git
    stdenv.cc.cc.lib
    autoPatchelfHook
  ];
  postInstall = ''
    mkdir -p $out/share/applications
    cp ${desktopItem}/share/applications/cn.classfabric.app.desktop $out/share/applications/cn.classfabric.app.desktop 
    mkdir -p $out/share/icons/hicolor/scalable/apps/
    cp ClassFabric.Desktop/Assets/AppLogo.svg $out/share/icons/hicolor/scalable/apps/cn.classfabric.app.svg
    printf deb > $out/lib/classfabric/PackageType
  '';
  postFixup = ''
    mv $out/bin/ClassFabric.Desktop $out/bin/classfabric
  '';
  packNupkg = false;
}
