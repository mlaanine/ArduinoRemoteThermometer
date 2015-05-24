#include <Servo.h>
Servo servo;
// PWM on pins 9 or 10 didn't work, this combination does.
const int servoPin = 11;
const int redLedPin = 5;
const int greenLedPin = 3; 
const int blueLedPin = 6;
// Use servo.writeMicroseconds to get finer control. Looks like servo.write (0...180)
// only takes the integer part, so that mapping from 255 to 180 makes for uneven steps.
// HS-422 servo should go from 700 to 2300 us. 600 to 2400 seems to work fine.
const int servoMin = 600;
const int servoMax = 2400;
    
void setup(){
  servo.attach(servoPin);
  Serial.begin(115200);
  pinMode(redLedPin, OUTPUT);
  pinMode(blueLedPin, OUTPUT);
}

void loop() {
  byte input;
  float uSec;
  if (Serial.available()) {
    input = Serial.read();
    // indicate out-of-range by dimming LED
    if (input == 0x0){
      servo.writeMicroseconds(servoMin);
      analogWrite(blueLedPin, 1);
      analogWrite(redLedPin, 0);
      analogWrite(greenLedPin, 0);
    }
    else if (input == 0xFF){
      servo.writeMicroseconds(servoMax);
      analogWrite(redLedPin, 1);
      analogWrite(blueLedPin, 0);
      analogWrite(greenLedPin, 0);
    }
    else {
      // Display range is 53°C: minimum is -20°C, maximum is +33°C.
      // Temperature is sent encoded in the byte with 0.2°C/bit and midpoint at 0x0F.
      uSec = (float)input * ((servoMax - servoMin) / 255.0) + servoMin;
      servo.writeMicroseconds(uSec);
      LightLed3(input);
//      Serial.print(input);
//      Serial.print(" ");
//      Serial.println(uSec);
    }
  }
}

void LightLed3(byte input){
  // Blue to yellow and yellow to red, constant brightness.
  // Use green LED at half brightness again.
  byte blue;
  byte red;
  byte green;
  
  if (input <= 127){
    red = input;
    green = red / 2;
    blue = 255 - input * 2;
  }
  else if (input <= 255){
    blue = 0;
    green = (255 - input) / 2;
    red = input;
  }
    
  analogWrite(redLedPin, red);
  analogWrite(blueLedPin, blue);
  analogWrite(greenLedPin, green);
}

