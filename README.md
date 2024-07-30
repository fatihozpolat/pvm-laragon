# PHP Version Manager [![version](https://img.shields.io/badge/version-1.0.0-blue.svg)]

## Intro
`pvm` allows you to quickly switch between PHP versions on your local machine.

It can also easily solve the Apache problem that occurs after PHP 8.

**Example:**
```bash
$ pvm use 8.3
🔗 Symlink created. PHP version set to 8.3
🆗 Path updated
🔄 Killing Laragon..
🚀 PHP version set to php-8.3.10-Win32-vs16-x64
🚀 Starting Laragon..

$ php -v
PHP 8.3.10 (cli) (built: Jul 30 2024 15:15:59) (ZTS Visual C++ 2019 x64)

$ pvm install 8.2
🤤 100%  -  31894993 / 31894993 bytes
👌 Download completed
📤 Extracted to C:\laragon\bin\php\php-8.2.22-Win32-vs16-x64
🆗 php.ini copied
🤤 100%  -  228633 / 228633 bytes
👌 Download completed
🆗 cacert.pem downloaded
🆗 php.ini updated
🎉 PHP installed successfully

$ pvm list
📦 Installed versions:
  7.4.33
  8.2.21
  8.2.22
  8.3.10

$ pvm remove 7.4
🗑️ Version removed

$ pvm help
Usage: pvm <command> [options]
Commands:
  install <version> - Install a specific version of PHP
  use <version> - Use a specific version of PHP
  list - List all installed versions of PHP
  list-remote - List all available versions of PHP
  remove <version> - Remove a specific version of PHP
  apache list - List all installed versions of Apache
  apache fix - Fix Apache installation
  apache use <version> - Use a specific version of Apache

$ pvm apache list
📦 Installed versions:
  httpd-2.4.54-win64-VS16
  httpd-2.4.62-win64-VS17

$ pvm apache fix
🤤 100%  -  11700671 / 11700671 bytes
👌 Download completed
📤 Extracted to C:\laragon\bin\apache\httpd-2.4.62-win64-VS17
🆗 Apache Installed
🔄 Killing Laragon..
🚀 Apache version set to httpd-2.4.62-win64-VS17
🚀 Starting Laragon..

$ pvm apache use 2.4.54
🔄 Killing Laragon..
🚀 Apache version set to httpd-2.4.54-win64-VS16
🚀 Starting Laragon..
```

It's that easy to use `pvm`!


## Installation
- Go to [Releases](https://github.com/fatihozpolat/pvm-laragon/releases) and download the pvm.zip file.
- Then run the pvm.exe in the file once. When you run this, it will add itself to the path and ask you for the laragon path. 
When you enter the laragon path, for example: C:\laragon, the application will close. Then close all terminal applications and open a new terminal and type pvm help.
- You can now delete the zip file you downloaded and start using pvm.


## Requirements
- [Laragon](https://laragon.org/download/) installed on your machine

## License
The scripts and documentation in this project are released under the [MIT License](LICENSE.md)