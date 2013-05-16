mooftpserv
==========

**mooftpserv** is an FTP server library written in **C# 2.0**. Its main goal is to provide an FTP server implementation for Windows CE with **.NET Compact Framework v3.5**. It also works with  **.NET v2.0** and **Mono 2.0**.

It has been tested with FileZilla, Total Commander, GNOME Nautilus, Windows Explorer, Firefox and Chrome as clients.

Usage
-----

This repository includes a small executable that can be directly used as a command-line FTP server.

    mooftpserv.exe [-h|--help] [-v|--verbose] [-p|--port <port>] [-b|--buffer <kbsize>]

**Options**
  - **-h | --help** Show the usage description
  - **-v | --verbose** More verbose logging
  - **-p | --port** TCP/IP server port on which to listen for new connections (default: 21)
  - **-b | --buffer** Size of the per-session data connection buffer (in kilobytes, default: 64)

The FTP server itself is implemented in a library, so that it can be embedded into other applications. Create an instance of `mooftpserv.Server` and call the synchronous `Run()` method to start the server.

Features
--------

The server implements only basic FTP commands. The following commands are supported (at least in some limited fashion):

    OPTS (only for OPTS UTF8 ON, which is always active anyway)
    SYST QUIT USER PASS FEAT
    TYPE PORT PASV
    PWD CWD CDUP
    MKD RMD RETR STOR DELE RNFR RNTO
    MDTM SIZE
    LIST
    STAT (only with a path as argument, where it acts like LIST, but over the control connection)
    NOOP

**FEAT** reports the following supported features:

    MDTM PASV SIZE TVFS UTF8

There are some options for customization when used as a library, made available as Properties of the `Server` class:

  - `LocalEndPoint`, `LocalAddress`, `LocalPort` to set the server address and port on which to listen for incoming connections (default: port 21, address 0.0.0.0 (or the first found non-loopback IP on Windows CE))
  - `BufferSize` to specify the size of the data connection buffer for each session (in kilobytes, default: 64)
  - `AuthHandler` to provide a handler for authenticating users and authorizing control and data connections (default: `mooftpserv.DefaultAuthHandler`, accepts username "anonymous" without password, allows all connections)
  - `FileSystemHandler` to provide a handler for file system access (default: `mooftpserv.DefaultFileSystemHandler`, implements and allows access to the real file system, on Unix starting at "/", on Windows CE starting at "\", and on Windows NT starting with all available drives)
  - `LogHandler` to provide a handler for log output (default: null)

Timestamps are always UTC. The control connection always uses UTF8, and the **LIST** response does too.

One thread per control connection is used. Data connections also run in that thread, thus blocking their control connections.

Server-to-server transfers are supported, and enabled by default. They can be disabled or restricted by allowing only data connections from the same address as the control connection, which can be implemented with an `IAuthHandler`.

License
-------

The source code is licensed under the **MIT license**. Its full text is included in the [LICENSE](LICENSE) file.
