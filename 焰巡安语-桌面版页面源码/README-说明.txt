焰巡安语 · 桌面版页面源码
==========================

先说结论：焰巡安语.exe 里没有网页代码。
它只有 68 KB，是一个启动器：
  1) 启动同目录的 node.exe + server/weather-api.js
  2) 用 Edge 的 --app 模式打开 http://127.0.0.1:8123/
  3) 关窗口时结束服务

真正的网页在这个文件夹里：
  unpackage\dist\build\h5\   ← 页面文件（本包 01-页面文件）
也就是 exe 打开的那个地址所返回的内容。

一、本包内容
------------
01-页面文件/
  index.html              浏览器收到的 HTML 外壳（23 行）
  assets/                 页面样式与脚本（构建产物，已压缩）
  文件对应关系见下表

02-启动器源码/
  Launcher.cs             exe 的全部源码（C#，启动服务 + 开 Edge 窗口）
  AssemblyInfo.cs         程序集信息

二、assets 文件对应哪个页面
--------------------------
pages-index-index.*.js      环境数据（首页）
pages-control-control.*.js  控制中心
pages-alarm-alarm.*.js      火情预警
pages-device-device.*.js    设备状态
pages-ai-ai.*.js            智能交互
pages-settings-settings.*.js 参数设置
pages-login-login.*.js      登录
index-IoHy40ZX.js           主包（Vue 运行时、公共逻辑、组件）
index-*.css / uni-*.css     全局与页面样式

注意：assets/*.js 是 Vite 压缩后的产物，函数名被缩短，只能读个大概，
不适合直接修改。真正可读、可改的源码是 .vue：
  C:\Users\Mood\Desktop\焰巡安语-Web源码\01-Vue源码\
（同目录还有 焰巡安语-Web源码.zip）

三、怎么让这份页面跑起来
------------------------
页面用 /assets/ 绝对路径，必须通过 HTTP 打开，不能双击 index.html：
  1) 进入 01-页面文件
  2) 用任意静态服务器，例如：
       python -m http.server 8080
  3) 浏览器打开 http://127.0.0.1:8080
天气/定位/AI 接口会失败（没有后端），页面有兜底，不影响看界面。

四、exe 的源码
--------------
见 02-启动器源码/Launcher.cs，重新编译：
  csc /target:winexe /win32icon:app.ico /out:焰巡安语.exe ^
      Launcher.cs AssemblyInfo.cs ^
      /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll

五、想改页面
------------
改 .vue → npm run build:h5 → 用新的 unpackage\dist\build\h5 覆盖
桌面版同目录下的同名文件夹，重启 exe 即可。
