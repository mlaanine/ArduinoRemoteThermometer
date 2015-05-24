# ArduinoRemoteThermometer
An excercise project in C#/WPF and Arduino.
The Windows app can fetch temperature data from the Finnish Meteorological Institute's (FMI) open data service
or from Weather Undeground.  If you use this code, PLEASE REPLACE MY API KEYS WITH YOUR OWN! Both are freely
available from the respective sites.
The Windows app sends the temperature data over a serial link to Arduino, which controls a servo to turn the
indicator of a physical thermometer. Add some nice lighting effects with an RGB LED. I used a SparkFun BlueSmirf
module to make this wireless.
