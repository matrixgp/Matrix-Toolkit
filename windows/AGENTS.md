# Windows application releases

The desktop application is named Matrix Toolkit. Keep the original Python toolkit separate.

For each new user-visible revision, increment `MatrixBuild` in `Directory.Build.props` once and add its entry to `CHANGELOG.md`. Rebuilding or testing the same revision must retain its number. Use this single property for the displayed build, file version, exports and ZIP names. Preserve previously released artifact folders.

Run the checks project and publish the self-contained Windows x64 application. Do not describe WPF visual behavior as tested unless it has actually run on Windows. Test network discovery against local loopback fixtures, rather than scanning the server's network.
