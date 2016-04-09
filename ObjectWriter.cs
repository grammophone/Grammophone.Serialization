using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;

namespace Grammophone.Serialization
{
	/// <summary>
	/// Writes an object graph to a stream.
	/// </summary>
	internal class ObjectWriter : ObjectStreamer
	{
		#region Auxilliary types

		/// <summary>
		/// Wraps references to objects.
		/// </summary>
		private struct Reference : IEquatable<Reference>
		{
			private object value;

			public Reference(object value)
			{
				this.value = value;
			}

			/// <summary>
			/// The referenced object.
			/// </summary>
			public object Value
			{
				get
				{
					return value;
				}
			}

			/// <summary>
			/// Returns a hash code using the base <see cref="Object.GetHashCode"/> 
			/// implemtation on the object in the <see cref="Value"/>
			/// field, even if the object has overriden the method.
			/// This means that the hash code depends on the pointer reference and not the contents of the object.
			/// </summary>
			public override int GetHashCode()
			{
				if (this.Value == null) return 0;

				return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this.Value);
			}

			/// <summary>
			/// Determines equality to another <see cref="Reference"/>
			/// based on reference equality of the <see cref="Value"/> properties.
			/// </summary>
			/// <param name="obj">The other reference to test for equality.</param>
			/// <returns>Returns true if the <see cref="Value"/> properties reference the same object instance.</returns>
			public override bool Equals(object obj)
			{
				return base.Equals((Reference)obj);
			}

			#region IEquatable<Reference> Members

			/// <summary>
			/// Determines equality to another <see cref="Reference"/>
			/// based on reference equality of the <see cref="Value"/> properties.
			/// </summary>
			/// <param name="other">The other reference to test for equality.</param>
			/// <returns>Returns true if the <see cref="Value"/> properties reference the same object instance.</returns>
			public bool Equals(Reference other)
			{
				return object.ReferenceEquals(this.Value, other.Value);
			}

			#endregion
		}

		#endregion

		#region Private members

		/// <summary>
		/// Maps types to type IDs.
		/// </summary>
		private Dictionary<Type, int> typesToIDsDictionary;

		/// <summary>
		/// Maps objects to object IDs.
		/// </summary>
		private Dictionary<Reference, int> objectsToIDsDictionary;

		/// <summary>
		/// Maps interned strings to their IDs.
		/// </summary>
		private Dictionary<string, int> stringsToIDsDictionary;

		/// <summary>
		/// If true, serialization will intern the field names of all objects.
		/// </summary>
		private bool internFieldNames;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="stream">The stream to read or write.</param>
		/// <param name="formatter">The formatter holding the serialization properties.</param>
		public ObjectWriter(Stream stream, FastBinaryFormatter formatter)
			: base(stream, formatter)
		{
			this.typesToIDsDictionary = new Dictionary<Type, int>();
			this.objectsToIDsDictionary = new Dictionary<Reference, int>();
			this.stringsToIDsDictionary = new Dictionary<string, int>();
			this.internFieldNames = formatter.InternFieldNames;
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Write an object graph to the stream.
		/// Repeatable calls will reuse object IDs and type IDs.
		/// </summary>
		/// <param name="value">The root of the object graph.</param>
		public void Write(object value)
		{
			WriteObject(value);
		}

		#endregion

		#region Private methods

		private void WriteString(string text)
		{
			if (text == null) throw new ArgumentNullException("text");
			
			var characters = text.ToCharArray();

			byte[] lengthBytes = BitConverter.GetBytes(characters.Length);

			stream.Write(lengthBytes, 0, 4);

			byte[] characterBytes = new byte[characters.Length * 2];

			int characterBytesPosition = 0;

			for (int i = 0; i < characters.Length; i++)
			{
				var character = characters[i];

				// Low byte
				characterBytes[characterBytesPosition++] = (byte)character;
				// High byte
				characterBytes[characterBytesPosition++] = (byte)(character >> 8);
			}

			stream.Write(characterBytes, 0, characterBytes.Length);

		}

		private void WriteInternedString(string text)
		{
			if (text == null) throw new ArgumentNullException("text");

			int id;

			if (this.stringsToIDsDictionary.TryGetValue(text, out id))
			{
				WriteInt32(id);
			}
			else
			{
				// Mark bit 31.
				id = (int)((uint)stringsToIDsDictionary.Count | 0x80000000);

				stringsToIDsDictionary[text] = id;

				WriteInt32(id);

				WriteString(text);
			}
		}

		private void WriteBoolean(Boolean value)
		{
			if (value)
				stream.WriteByte(1);
			else
				stream.WriteByte(0);

		}

		private void WriteByte(Byte value)
		{
			stream.WriteByte(value);
		}

		private void WriteChar(Char value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 2);
		}

		private void WriteDateTime(DateTime value)
		{
			WriteInt64(value.Ticks);
		}

		private void WriteDecimal(Decimal value)
		{
			var bits = Decimal.GetBits(value);

			WriteInt32(bits[0]);
			WriteInt32(bits[1]);
			WriteInt32(bits[2]);
			WriteInt32(bits[3]);
		}

		private void WriteDouble(Double value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 8);
		}

		private void WriteInt16(Int16 value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 2);
		}

		private void WriteInt32(Int32 value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 4);
		}

		private void WriteInt64(Int64 value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 8);
		}

		private void WriteSByte(SByte value)
		{
			stream.WriteByte((byte)value);
		}

		private void WriteSingle(Single value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 4);
		}

		private void WriteTimeSpan(TimeSpan value)
		{
			WriteInt64(value.Ticks);
		}

		private void WriteUInt16(UInt16 value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 2);
		}

		private void WriteUInt32(UInt32 value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 4);
		}

		private void WriteUInt64(UInt64 value)
		{
			var bytes = BitConverter.GetBytes(value);

			stream.Write(bytes, 0, 8);
		}

		/// <summary>
		/// Write an object to the stream and its fields recursively.
		/// The object may be a reference type, including an array, or a value type.
		/// The graph traversal pattern is DFS.
		/// </summary>
		/// <param name="value">The object to write.</param>
		private void WriteObject(object value)
		{
			if (value == null)
			{
				WriteByte((byte)ElementType.Null);

				return;
			}

			var type = value.GetType();

			if (!type.IsValueType)
			{
				int objectID;

				if (objectsToIDsDictionary.TryGetValue(new Reference(value), out objectID))
				{
					WriteByte((byte)ElementType.ObjectRef);
					WriteInt32(objectID);

					return;
				}
			}

			if (!type.IsArray)
			{
				WriteByte((byte)ElementType.Instance);

				var surrogate = GetSurrogateFor(type);

				if (surrogate != null)
				{
					var serializationInfo = new SerializationInfo(type, converter);

					FireSerializationEvent(value, typeof(OnSerializingAttribute));

					surrogate.GetObjectData(value, serializationInfo, context);

					WriteSerializationInfo(value, serializationInfo);

					FireSerializationEvent(value, typeof(OnSerializedAttribute));

					return;
				}

				var serializableImplementation = value as ISerializable;

				if (serializableImplementation != null)
				{
					var serializationInfo = new SerializationInfo(type, converter);

					FireSerializationEvent(value, typeof(OnSerializingAttribute));

					serializableImplementation.GetObjectData(serializationInfo, context);

					WriteSerializationInfo(value, serializationInfo);

					FireSerializationEvent(value, typeof(OnSerializedAttribute));

					return;
				}

				WriteType(type);

				if (type.IsValueType)
				{
					if (type.IsEnum)
					{
						var enumUnderlyingType = type.GetEnumUnderlyingType();

						if (enumUnderlyingType == typeof(Byte))
						{
							WriteByte(Convert.ToByte(value));
						}
						else if (enumUnderlyingType == typeof(Int16))
						{
							WriteInt16(Convert.ToInt16(value));
						}
						else if (enumUnderlyingType == typeof(Int32))
						{
							WriteInt32(Convert.ToInt32(value));
						}
						else if (enumUnderlyingType == typeof(Int64))
						{
							WriteInt64(Convert.ToInt64(value));
						}
						else if (enumUnderlyingType == typeof(SByte))
						{
							WriteSByte(Convert.ToSByte(value));
						}
						else if (enumUnderlyingType == typeof(UInt16))
						{
							WriteUInt16(Convert.ToUInt16(value));
						}
						else if (enumUnderlyingType == typeof(UInt32))
						{
							WriteUInt32(Convert.ToUInt32(value));
						}
						else if (enumUnderlyingType == typeof(UInt64))
						{
							WriteUInt64(Convert.ToUInt64(value));
						}
						else
						{
							throw new SerializationException(
								String.Format("Unsupported underlying type '{0}' of enum type '{1}'.", enumUnderlyingType.FullName, type.FullName));
						}

						return;
					}
					if (type == typeof(Boolean))
					{
						WriteBoolean((Boolean)value);
						return;
					}
					else if (type == typeof(Byte))
					{
						WriteByte((Byte)value);
						return;
					}
					else if (type == typeof(Char))
					{
						WriteChar((Char)value);
						return;
					}
					else if (type == typeof(DateTime))
					{
						WriteDateTime((DateTime)value);
						return;
					}
					else if (type == typeof(Decimal))
					{
						WriteDecimal((Decimal)value);
						return;
					}
					else if (type == typeof(Double))
					{
						WriteDouble((Double)value);
						return;
					}
					else if (type == typeof(Int16))
					{
						WriteInt16((Int16)value);
						return;
					}
					else if (type == typeof(Int32))
					{
						WriteInt32((Int32)value);
						return;
					}
					else if (type == typeof(Int64))
					{
						WriteInt64((Int64)value);
						return;
					}
					else if (type == typeof(SByte))
					{
						WriteSByte((SByte)value);
						return;
					}
					else if (type == typeof(Single))
					{
						WriteSingle((Single)value);
						return;
					}
					else if (type == typeof(TimeSpan))
					{
						WriteTimeSpan((TimeSpan)value);
						return;
					}
					else if (type == typeof(UInt16))
					{
						WriteUInt16((UInt16)value);
						return;
					}
					else if (type == typeof(UInt32))
					{
						WriteUInt32((UInt32)value);
						return;
					}
					else if (type == typeof(UInt64))
					{
						WriteUInt64((UInt64)value);
						return;
					}
					else if (type.IsEnum)
					{
						var enumUnderlyingType = type.GetEnumUnderlyingType();
					}
				}
				else // if not a value type...
				{
					WriteObjectID(value);
				}

				if (type == typeof(string))
				{
					WriteString((string)value);

					return;
				}

				FireSerializationEvent(value, typeof(OnSerializingAttribute));

				var serializableMemberInfos = GetSerializableMembersViaReflection(type);

				WriteInt32(serializableMemberInfos.Length);

				var serializableMemberValues = FormatterServices.GetObjectData(value, serializableMemberInfos);

				for (int i = 0; i < serializableMemberInfos.Length; i++)
				{
					string memberName = serializableMemberInfos[i].Name;

					if (internFieldNames)
						WriteInternedString(memberName);
					else
						WriteString(memberName);

					WriteObject(serializableMemberValues[i]);
				}

				FireSerializationEvent(value, typeof(OnSerializedAttribute));

			}
			else
			{
				WriteByte((byte)ElementType.Array);

				var elementType = type.GetElementType();

				WriteType(elementType);

				WriteObjectID(value);

				var array = (Array)value;

				WriteInt32(array.Rank);

				for (int dimension = 0; dimension < array.Rank; dimension++)
				{
					var lowerBound = array.GetLowerBound(dimension);
					var length = array.GetLength(dimension);

					WriteInt32(lowerBound);
					WriteInt32(length);
				}

				VisitArrayElements(array, (arr, indices) => WriteObject(arr.GetValue(indices)));
			}
		}

		/// <summary>
		/// Write the object ID of an object. 
		/// If the object is a reference type, a new dictionary entry with the new object ID is allocated 
		/// associated with the object, else if it is a value type a -1 is recorded.
		/// </summary>
		/// <param name="value">The object for which to record an object ID.</param>
		private void WriteObjectID(object value)
		{
			int objectID;

			if (!value.GetType().IsValueType)
			{
				objectID = objectsToIDsDictionary.Count;

				try
				{
					objectsToIDsDictionary.Add(new Reference(value), objectID);
				}
				catch (ArgumentException ex)
				{
					throw new SerializationException("The object already exists in the object ID dictionary.", ex);
				}
			}
			else
			{
				objectID = -1;
			}

			WriteInt32(objectID);
		}

		/// <summary>
		/// Write the <see cref="SerializationInfo"/> as composed by <see cref="ISerializable"/> 
		/// and <see cref="ISerializationSurrogate"/> implementations.
		/// </summary>
		/// <param name="value">The object being serialized.</param>
		/// <param name="info">The container of the serialized values.</param>
		private void WriteSerializationInfo(object value, SerializationInfo info)
		{
			WriteType(info.ObjectType);

			if (!value.GetType().IsValueType) WriteObjectID(value);

			WriteInt32(info.MemberCount);

			var enumerator = info.GetEnumerator();

			while (enumerator.MoveNext())
			{
				var entry = enumerator.Current;

				if (internFieldNames)
					WriteInternedString(entry.Name);
				else
					WriteString(entry.Name);
				
				WriteObject(entry.Value);
			}
		}

		/// <summary>
		/// Writes a <see cref="ElementType.Type"/> or <see cref="ElementType.TypeRef"/> element depending on
		/// whether the type has been previously encountered or not.
		/// </summary>
		/// <param name="type">The type to write to the stream.</param>
		/// <remarks>
		/// Takes into account the <see cref="SerializationBinder"/> associated with the formatter.
		/// </remarks>
		/// <exception cref="SerializationException">
		/// When the type or at least one of its ancestors is not marked as serializable.
		/// </exception>
		private void WriteType(Type type)
		{
			int typeID;

			if (typesToIDsDictionary.TryGetValue(type, out typeID))
			{
				WriteByte((byte)ElementType.TypeRef);
				WriteInt32(typeID);

				return;
			}

			EnsureTypeIsSerializable(type);

			typeID = typesToIDsDictionary.Count;
			typesToIDsDictionary.Add(type, typeID);

			WriteByte((byte)ElementType.Type);
			WriteInt32(typeID);

			string typeName = null;
			string assemblyName = null;

			binder.BindToName(type, out assemblyName, out typeName);

			if (assemblyName == null) assemblyName = type.Assembly.FullName;
			if (typeName == null) typeName = type.FullName;

			WriteString(assemblyName);
			WriteString(typeName);
		}

		#endregion
	}
}
