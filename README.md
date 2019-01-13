# Xiropht-Proxy-Solo-Miner
Xiropht Proxy Solo Miner is usefull for share different part of the current block to other miners.

**Be carefull , we are currently in phase of test , all versions released and uploaded are compiled in debug mode for permit everyone to get informations of possible issues from log files until to release the main phase network.**

**Once the main phase network is released, all next update will are in release mode for disable log file and debug mode.**

**In production, we suggest to compile in Release Mode for disable log files.**

**Compatibility:** Windows (Visual Studio), Linux (Mono), Android (with Xamarin or Mono), MacOSX (with Xamarin or Mono)


**Features:**

-You can set a different name to each miners on their setting file for check their status, only the wallet address is important to be use on the proxy side. 

- You can select also the range on your miners side for spread efforts to each miners.

- The proxy solo miner can reconnect automaticaly to the network.

- Send confirmation to each miners when one of them found a block.


**Linux**:

If the linux binary don't work you can compile the Windows version yourself with the package: mono-complete.
Follow this instruction for make your own linux binary:

mkbundle Xiropht-Proxy-Solo-Miner.exe -o Xiropht-Proxy-Solo-Miner Xiropht-Connector-All.dll NCalc.dll Antlr3.Runtime.dll --deps -z --static



