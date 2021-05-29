# **SerialTerm**

## Simple serial port utility. Designed to be used with Windows Terminal.

Serial Term is a console application designed to interact with Serial Ports under Windows 10. It can be easily integrated with the Windows Terminal application.

In the market, there are a lot of options but some of them are to much complex and others are simple but based on windows dialogs with some limitations. The purpose of Serial Term is to have something simple but integrated with the Windows Terminal to have it at hand when it is needed. To start a new session open Serial Term type '.open', chose the desired values, and you are done. All the configurations can be performed using some basic commands.

To send some data, type it followed by an 'Enter'. Use escape characters to send any special character like '\\n'(LF) or '\\r'(CR). To send data that starts with a dot scape it '\\.'.

If you want to send some binary data, switch to hex/bin mode then you can type it in Hex format like:

9003912399005645342087560976

Start it with:

**SerialTerm.exe** [Port][Baud rate][Data bits][Parity][Stop bits][Handshake]

Supported values
 - Baud rate: 300, 600, 1200, 2400, 4800, 9600*, 14400, 19200, 38400, 57600, 115200
 - Data bits: 5, 6, 7, 8*
 - Parity: None*, Odd, Even, Mark, Space
 - Stop bits: None, One*, Two, OnePointFive
 - Handshake: None*, XOnXOff, RequestToSend, RequestToSendXOnXOff
  *Default values

Interactive commands, available at any time:

**.open** [Port][Baud rate][Data bits][Parity][Stop bits][Handshake]\
  Open a new connection using provided values.

**.close**\
  Close current connection.

**.send** filename\
  Send a file.

**.hex|.bin**\
  Input/output in binary hexadecimal mode.

**.asc|.text**\
  Input/output in ASCII mode.

**.color** [received text color] [send text color]\
  Change the color of send/received text. Available colors Black, DarkBlue, DarkGreen, DarkCyan, DarkRed,
  DarkMagenta, DarkYellow, Gray, DarkGray, Blue, Green, Cyan, Red, Magenta, Yellow, White.

**.exit**\
  Exit from SerialTerm.

**.help**\
  Print this help.

Any contribution is welcome, also if you want some new feature don't hesitate to ask about it. 