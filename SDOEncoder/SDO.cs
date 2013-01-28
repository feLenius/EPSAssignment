using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

//TODO - sudėtinius tipus, atsižvelti į BitConverter.IsLittleEndian, nežinomų tipų deserializacija, pridėti klaidų gaudymą/apdorojimą

namespace SDOSerializer
{
    public class SDO
    {
        /// <summary>
        /// Serializes given object to byte array
        /// </summary>
        /// <param name="obj">object to serialize</param>
        /// <returns>Object, serialized to byte array</returns>
        public byte[] Serialize(object obj)
        {
            List<byte> serializedObject = new List<byte>();

            //if object has properties - is reference type
            PropertyInfo[] properties = obj.GetType().GetProperties();
            if (properties.Length > 0 && obj.GetType() != typeof(string))
            {
                foreach (PropertyInfo pi in properties)
                {
                    serializedObject.AddRange(SerializeValueType(pi.GetValue(obj), pi.Name));
                }
            }
            //object is reference type
            else
                serializedObject.AddRange(SerializeValueType(obj, "1"));

            return serializedObject.ToArray();
        }


        /// <summary>
        /// Deserializes object to given type
        /// </summary>
        /// <param name="data">Byte array containing serialized object</param>
        /// <param name="objType">Object type</param>
        /// <returns>Object of given type</returns>
        public object Deserialize(byte[] data, Type objType)
        {
            //create instance of given type object
            object generetedObject;
            if (objType != typeof(string))
                generetedObject = Activator.CreateInstance(objType);
            else
                generetedObject = "";

            int dataIndex = 0;
            while (dataIndex < data.Length)
            {
                //parse name of the property
                //it's stored in SDO element tag
                string name = "";
                byte tag = data[dataIndex];
                dataIndex++;
                if (tag > 0xF8)
                {
                    int nameLength = tag - 0xF8;
                    byte[] nameBytes = new byte[nameLength];
                    Array.Copy(data, dataIndex, nameBytes, 0, nameLength);
                    name = Encoding.UTF8.GetString(nameBytes, 0, nameBytes.Length);
                    dataIndex += nameLength;
                }

                //parse value by type
                byte type = data[dataIndex];
                switch (type)
                {
                    //bool true
                    case 0x20:
                        SetObjectPropertyValue(name, true, ref generetedObject);
                        dataIndex++;
                        break;

                    //bool false
                    case 0x40: 
                        SetObjectPropertyValue(name, false, ref generetedObject);
                        dataIndex++;
                        break;

                    //Int16
                    case 0x22:
                        Int16 int16Value = BitConverter.ToInt16(data, dataIndex + 1);
                        SetObjectPropertyValue(name, int16Value, ref generetedObject);
                        dataIndex += 3; //type + value ilgiai
                        break;

                    //Int32
                    case 0x24:
                        Int32 int32Value = BitConverter.ToInt32(data, dataIndex + 1);
                        SetObjectPropertyValue(name, int32Value, ref generetedObject);
                        dataIndex += 5;
                        break;

                    //Int64
                    case 0x28:
                        Int64 int64Value = BitConverter.ToInt64(data, dataIndex + 1);
                        SetObjectPropertyValue(name, int64Value, ref generetedObject);
                        dataIndex += 9;
                        break;

                    //UInt16
                    case 0x42:
                        UInt16 uint16Value = BitConverter.ToUInt16(data, dataIndex + 1);
                        SetObjectPropertyValue(name, uint16Value, ref generetedObject);
                        dataIndex += 3;
                        break;

                    //UInt32
                    case 0x44:
                        UInt32 uint32Value = BitConverter.ToUInt32(data, dataIndex + 1);
                        SetObjectPropertyValue(name, uint32Value, ref generetedObject);
                        dataIndex += 5;
                        break;

                    //UInt64
                    case 0x48:
                        UInt64 uint64Value = BitConverter.ToUInt64(data, dataIndex + 1);
                        SetObjectPropertyValue(name, uint64Value, ref generetedObject);
                        dataIndex += 9;
                        break;

                    //Guid
                    case 0x50:
                        byte[] guidBytes = new byte[16];
                        Array.Copy(data, dataIndex + 1, guidBytes, 0, 16);
                        Guid guid = new Guid(guidBytes);
                        SetObjectPropertyValue(name, guid, ref generetedObject);
                        dataIndex += 17;
                        break;

                    default:
                        //string
                        if (type > 0x60 && type <= 0x7F)
                        {
                            dataIndex++;
                            int stringByteCount = 0;
                            //if Li == 0, last 4 bits shows string length in bytes
                            if (type < 0x70)
                                stringByteCount = type - 0x60;
                            //else, if Li == 1, last 4 bits shows, how many following bytes used to store string length
                            else
                            {
                                int stringLengthByteCount = type - 0x70;
                                byte[] stringLengthInBytes = new byte[stringLengthByteCount];
                                Array.Copy(data, dataIndex, stringLengthInBytes, 0, stringLengthByteCount);
                                byte[] stringLength = new byte[4];
                                for (int i = 0; i < stringLengthInBytes.Length; i++)
                                {
                                    stringLength[i] = stringLengthInBytes[i];
                                }
                                stringByteCount = BitConverter.ToInt32(stringLength,0);
                                dataIndex += stringLengthByteCount;
                            }
                            SetObjectPropertyValue(name, Encoding.UTF8.GetString(data, dataIndex, stringByteCount), ref generetedObject);
                            dataIndex += stringByteCount;
                        }
                        //undefined - array of bytes
                        else if (type > 0x00 && type <= 0x1F)
                        {
                            //TO-DO
                        }
                        break;
                }
            }
            return generetedObject;
        }

        /// <summary>
        /// Set Value of object
        /// </summary>
        /// <param name="propName">Name of the Property</param>
        /// <param name="value">Value to set</param>
        /// <param name="obj">Reference to object, that contains given property</param>
        private void SetObjectPropertyValue(string propName, object value, ref object obj)
        {
            PropertyInfo propInfo = obj.GetType().GetProperty(propName);
            if (propInfo != null)
                propInfo.SetValue(obj, value);
            //if object does not have given property, maybe its value type object?
            else
            {
                Type objectType = obj.GetType();
                List<Type> knownTypes = new List<Type>() { typeof(Boolean),
                                                           typeof(Int16),
                                                           typeof(Int32),
                                                           typeof(Int64),
                                                           typeof(UInt16),
                                                           typeof(UInt32),
                                                           typeof(UInt64),
                                                           typeof(Int16),
                                                           typeof(Guid),
                                                           typeof(String)};
                if (knownTypes.Contains(objectType))
                    obj = value;
            }
        }

        /// <summary>
        /// Serializes Value Type object
        /// </summary>
        /// <param name="obj">Value Type object</param>
        /// <param name="name">Name of the object/property</param>
        /// <returns>Serialized value type object</returns>
        private byte[] SerializeValueType(object obj, string name)
        {
            if (obj == null)
                return new byte[0];

            //Value name must be up to 7 chars
            if (name.Length > 7)
                name = name.Substring(0, 7);
            byte[] tag = new byte[1+name.Length];
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte tagHeader = BitConverter.GetBytes(0xF8 + nameBytes.Length)[0];
            tag[0] = tagHeader;
            Array.Copy(nameBytes, 0, tag, 1, nameBytes.Length);

            //serialize value
            byte[] type;
            byte[] serializedValue;
            Type objectType = obj.GetType();
            switch (objectType.Name)
            {
                case "Boolean": if ((bool)obj)
                                    type = new byte[] { 0x20 }; 
                                else
                                    type = new byte[] { 0x40 };
                                serializedValue = new byte[0];
                                break;

                case "Int16":   type = new byte[] { 0x22 };
                                serializedValue = BitConverter.GetBytes((Int16)obj); 
                                //pries atkomentuojant sudeti visur tikrinimus...
                                //if (BitConverter.IsLittleEndian) 
                                //    Array.Reverse(serializedValue); 
                                break;

                case "Int32":   type = new byte[] { 0x24 };
                                serializedValue = BitConverter.GetBytes((Int32)obj); 
                                //if (BitConverter.IsLittleEndian) 
                                //    Array.Reverse(serializedValue); 
                                break;

                case "Int64":   type = new byte[] { 0x28 };
                                serializedValue = BitConverter.GetBytes((Int64)obj); 
                                //if (BitConverter.IsLittleEndian) 
                                //    Array.Reverse(serializedValue); 
                                break;

                case "UInt16":  type = new byte[] { 0x42 };
                                serializedValue = BitConverter.GetBytes((UInt16)obj); 
                                //if (BitConverter.IsLittleEndian) 
                                //    Array.Reverse(serializedValue); 
                                break;

                case "UInt32":  type = new byte[] { 0x44 };
                                serializedValue = BitConverter.GetBytes((UInt32)obj); 
                                //if (BitConverter.IsLittleEndian) 
                                //    Array.Reverse(serializedValue); 
                                break;

                case "UInt64":  type = new byte[] { 0x48 }; 
                                serializedValue = BitConverter.GetBytes((UInt64)obj); 
                                //if (BitConverter.IsLittleEndian) 
                                //    Array.Reverse(serializedValue); 
                                break;

                case "Guid":    type = new byte[] { 0x50 };
                                serializedValue = ((Guid)obj).ToByteArray(); 
                                break;

                case "String":  serializedValue = Encoding.UTF8.GetBytes((string)obj);
                                int stringLength = serializedValue.Length;
                                //To-Do atsizvelgti i IsLittlEndian
                                //if string length is equal or less than 15 chars, it's length is saved in type byte
                                if (stringLength <= 15)
                                    type = new byte[] { BitConverter.GetBytes(0x60 + stringLength)[0] };
                                //if string length is more than 15 chars, it's length is saved in following bytes
                                else
                                {
                                    int valueLengthByteCount = (int)Math.Ceiling((double)stringLength / 0xFF);
                                    byte typeHeader = BitConverter.GetBytes(0x70 + valueLengthByteCount)[0];
                                    byte[] stringByteCount = BitConverter.GetBytes(stringLength);
                                    type = new byte[1 + valueLengthByteCount];
                                    type[0] = typeHeader;
                                    Array.Copy(stringByteCount, 0, type, 1, valueLengthByteCount);
                                }
                                break;

                default:        BinaryFormatter bf = new BinaryFormatter();
                                MemoryStream ms = new MemoryStream();
                                bf.Serialize(ms, obj);
                                serializedValue = ms.ToArray();
                                int valueLength = serializedValue.Length;
                                //To-Do atsizvelgti i IsLittlEndian
                                if (valueLength <= 15)
                                    type = new byte[] { BitConverter.GetBytes(0x00+valueLength)[0] };
                                else
                                {
                                    int valueLengthByteCount = (int)Math.Ceiling((double)valueLength / 0xFF);
                                    byte typeHeader = BitConverter.GetBytes(0x01 + valueLengthByteCount)[0];
                                    byte[] valueByteCount = BitConverter.GetBytes(valueLength);
                                    type = new byte[1 + valueLengthByteCount];
                                    type[0] = typeHeader;
                                    Array.Copy(valueByteCount, 0, type, 1, valueLengthByteCount);
                                }
                                break;
            }

            //compose serialized object
            byte[] serializedObject = new byte[tag.Length + type.Length + serializedValue.Length];
            Array.Copy(tag, 0, serializedObject, 0, tag.Length);
            Array.Copy(type, 0, serializedObject, tag.Length, type.Length);
            Array.Copy(serializedValue, 0, serializedObject, tag.Length + type.Length, serializedValue.Length);
            return serializedObject;
        }
    }
}
