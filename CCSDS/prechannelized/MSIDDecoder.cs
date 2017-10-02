namespace gov.nasa.arc.ccsds.prechannelized
{
    public class MSIDDecoder
    {
        public static string Subsystem(string label)
        {
            return label == null ? "Unknown subsystem" : Subsystem(label[0]);
        }

        public static string Subsystem(char c)
        {
            switch (c)
            {
                case 'N':
                    return "NSS";
                case 'I':
                    return "NIRVSS";
                case 'F':
                    return "Fluid System";
                case 'O':
                    return "OVEN";
                case 'D':
                    return "Drill";
                case 'C':
                    return "Drill Ops Camera";
                case 'A':
                    return "Avionics";
                case 'S':
                    return "Software";
                case 'R':
                    return "NIRST";
                case 'W':
                    return "Water Droplet Camera";
                case 'M':
                    return "MS";
                case 'V':
                    return "Oven Ops Camera";
                case 'G':
                    return "GC";
                default:
                    return "Unrecognized subsystem";
            }
        }

        public static string Class(string label)
        {
            return label == null ? "Unknown Class" : Class(label[1]);
        }

        public static string Class(char c)
        {
            switch (c)
            {
                case 'H':
                    return "Heater";
                case 'V':
                    return "Valve";
                case 'M':
                    return "Motor";
                case 'E':
                    return "Encoder";
                case 'N':
                    return "Potentiometer";
                case 'T':
                    return "Temperature Sensor";
                case 'P':
                    return "Pressure Sensor";
                case 'G':
                    return "Generic End Item";
                case 'X':
                    return "Pseudo End Item";
                case 'L':
                    return "Voltage Sensor";
                case 'A':
                    return "Current Sensor";
                case 'O':
                    return "Cooler (TEC)";
                case 'R':
                    return "Humidity Sensor";
                case 'W':
                    return "Power Switch";
                case 'C':
                    return "Load Cell";
                case 'S':
                    return "Solenoid";
                case 'Y':
                    return "Rotary Solenoid";
                case 'K':
                    return "Brake";
                case 'I':
                    return "Switch Indicator";
                default:
                    return "Unrecognized End Item Type";
            }
        }

        public static string Kind(string label)
        {
            return label == null ? "Unknown Kind" : Kind(label[2]);
        }

        public static string Kind(char c)
        {
            switch (c)
            {
                case 'K':
                    return "Command";
                case 'V':
                    return "Telemetry";
                default:
                    return "Unrecognized Kind";
            }
        }

        public static string SerialNo(string label)
        {
            return label == null ? "Unknown Serial Number" : label.Substring(3, 3);
        }

        public static string Units(string label)
        {
            return label == null ? "Unknown Units" : Units(label[6]);
        }

        public static string Units(char c)
        {
            switch (c)
            {
                case 'T':
                    return "Degrees C (Temperature)";
                case 'P':
                    return "kPA? (Pressure)";
                case 'V':
                    return "Voltage";
                case 'A':
                    return "Amperes";
                case 'Z':
                    return "Resistance";
                case 'G':
                    return "Grams";
                case 'F':
                    return "Newtons";
                case 'K':
                    return "Energy - keV";
                case 'R':
                    return "Motor State";
                case 'M':
                    return "Motor Counts";
                case 'W':
                    return "Power Level";
                case 'S':
                    return "Motor Speed";
                case 'Q':
                    return "Motor Acceleration";
                case 'O':
                    return "Motor Operation";
                case 'E':
                    return "Motor Encoder Absolute Position";
                case 'L':
                    return "Motor Relative Position";
                case 'B':
                    return "Discrete Indicator";
                case 'I':
                    return "Generic Integer";
                case 'N':
                    return "Generic Analog";
                case 'H':
                    return "Relative Humidity";
                default:
                    return "Unknown units";
            }
        }
    }
}