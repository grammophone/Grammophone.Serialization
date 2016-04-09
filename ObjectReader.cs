using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.IO;
using System.Reflection;

namespace Grammophone.Serialization
{
	/// <summary>
	/// Reads an object graph from a stream.
	/// </summary>
	internal class ObjectReader : ObjectStreamer
	{
		#region Private fields

		/// <summary>
		/// List of the callbacks to be invoked upon deserialization completion.
		/// </summary>
		private List<IDeserializationCallback> deserializationCallbacksList;

		/// <summary>
		/// Maps type IDs to types.
		/// </summary>
		private List<Type> idsToTypesList;

		/// <summary>
		/// Maps object IDs to objects.
		/// </summary>
		private List<object> idsToObjectsList;

		/// <summary>
		/// While deserializing using <see cref="ISerializationSurrogate"/> or <see cref="IObjectReference"/>
		/// mechanisms, this set set holds the surrogated objects that have not been used during recursive deserialization.
		/// </summary>
		/// <remarks>
		/// This set is used to track the unsupported scenario of <see cref="FastBinaryFormatter"/>.
		/// If an initial surrogated type was referenced during its recursive
		/// deserialization and the <see cref="ISerializationSurrogate.GetObjectData"/>
		/// returned a different object, then no reference fixup is possible with this formatter.
		/// The graph would have uninitialized left-overs of the initial surrogated object.
		/// Using this set we track the problem and we fire an exception.
		/// </remarks>
		private ISet<int> unusedObjectIDsDuringSurrogation;

		/// <summary>
		/// Cache for deserialization constructors of types implementing <see cref="ISerializable"/>.
		/// </summary>
		private Dictionary<Type, ConstructorInfo> typesToDeserializationConstructors;
		
		/// <summary>
		/// Cache for default constructors.
		/// </summary>
		private Dictionary<Type, ConstructorInfo> typesToDefaultConstructors;

		/// <summary>
		/// Holds a cache of whether member infos are marked as optional
		/// via the <see cref="OptionalFieldAttribute"/>.
		/// </summary>
		private Dictionary<MemberInfo, bool> memberInfoOptionalityCache;

		/// <summary>
		/// Keeps interned strings by their IDs, after clearing bit 31 of their ID.
		/// </summary>
		private List<string> idsToStringsList;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="formatter">The formatter holding the serialization properties.</param>
		public ObjectReader(Stream stream, FastBinaryFormatter formatter)
			: base(stream, formatter)
		{
			deserializationCallbacksList = new List<IDeserializationCallback>();
			idsToTypesList = new List<Type>();
			idsToObjectsList = new List<object>();
			unusedObjectIDsDuringSurrogation = new HashSet<int>();
			typesToDeserializationConstructors = new Dictionary<Type, ConstructorInfo>();
			typesToDefaultConstructors = new Dictionary<Type, ConstructorInfo>();
			memberInfoOptionalityCache = new Dictionary<MemberInfo, bool>();
			idsToStringsList = new List<string>();
		}
		
		#endregion

		#region Public methods

		/// <summary>
		/// Read an object graph from the stream.
		/// Repeatable calls will reuse object IDs and type IDs defined in previous reads.
		/// </summary>
		/// <returns>
		/// Returns the object graph.
		/// </returns>
		public object Read()
		{
			var value = ReadObject();

			for (int i = 0; i < deserializationCallbacksList.Count; i++)
			{
				deserializationCallbacksList[i].OnDeserialization(this);
			}

			deserializationCallbacksList.Clear();

			return value;
		}

		#endregion

		#region Private methods

		private string ReadString()
		{
			byte[] lengthBytes = new byte[4];

			ExpectFromStream(lengthBytes, 0, 4);

			int length = BitConverter.ToInt32(lengthBytes, 0);

			if (length < 0)
			{
				// The string is interned.
				int positiveID = length & 0x7FFFFFFF;

				if (positiveID < idsToStringsList.Count) return idsToStringsList[positiveID];

				string text = ReadString();

				AccommodateList(idsToStringsList, positiveID);

				idsToStringsList[positiveID] = text;

				return text;
			}

			if (length == 0) return String.Empty;

			byte[] characterBytes = new byte[2 * length];

			ExpectFromStream(characterBytes, 0, 2 * length);

			int characterBytePosition = 0;

			var characters = new char[length];

			for (int i = 0; i < characters.Length; i++)
			{
				byte lowByte = characterBytes[characterBytePosition++];
				byte highByte = characterBytes[characterBytePosition++];

				char character = (char)(lowByte | ((int)highByte << 8));

				characters[i] = character;
			}

			return new String(characters);
		}

		private Boolean ReadBoolean()
		{
			var byteValue = ExpectByteFromStream();

			return byteValue != 0;
		}

		private Byte ReadByte()
		{
			return ExpectByteFromStream();
		}

		private Char ReadChar()
		{
			var bytes = new byte[2];

			ExpectFromStream(bytes, 0, 2);

			return BitConverter.ToChar(bytes, 0);
		}

		private DateTime ReadDateTime()
		{
			return DateTime.FromBinary(ReadInt64());
		}

		private Decimal ReadDecimal()
		{
			int[] bits = new int[4];

			bits[0] = ReadInt32();
			bits[1] = ReadInt32();
			bits[2] = ReadInt32();
			bits[3] = ReadInt32();

			return new Decimal(bits);
		}

		private Double ReadDouble()
		{
			var bytes = new byte[8];

			ExpectFromStream(bytes, 0, 8);

			return BitConverter.ToDouble(bytes, 0);
		}

		private Int16 ReadInt16()
		{
			var bytes = new byte[2];

			ExpectFromStream(bytes, 0, 2);

			return BitConverter.ToInt16(bytes, 0);
		}

		private Int32 ReadInt32()
		{
			var bytes = new byte[4];

			ExpectFromStream(bytes, 0, 4);

			return BitConverter.ToInt32(bytes, 0);
		}

		private Int64 ReadInt64()
		{
			var bytes = new byte[8];

			ExpectFromStream(bytes, 0, 8);

			return BitConverter.ToInt64(bytes, 0);
		}

		private SByte ReadSByte()
		{
			return (SByte)ExpectByteFromStream();
		}

		private Single ReadSingle()
		{
			var bytes = new byte[4];

			ExpectFromStream(bytes, 0, 4);

			return BitConverter.ToSingle(bytes, 0);
		}

		private TimeSpan ReadTimeSpan()
		{
			return new TimeSpan(ReadInt64());
		}

		private UInt16 ReadUInt16()
		{
			var bytes = new byte[2];

			ExpectFromStream(bytes, 0, 2);

			return BitConverter.ToUInt16(bytes, 0);
		}

		private UInt32 ReadUInt32()
		{
			var bytes = new byte[4];

			ExpectFromStream(bytes, 0, 4);

			return BitConverter.ToUInt32(bytes, 0);
		}

		private UInt64 ReadUInt64()
		{
			var bytes = new byte[8];

			ExpectFromStream(bytes, 0, 8);

			return BitConverter.ToUInt64(bytes, 0);
		}

		/// <summary>
		/// Read an object from the stream and its fields recursively.
		/// </summary>
		private object ReadObject()
		{
			var elementInStream = (ElementType)ReadByte();

			int objectID;

			switch (elementInStream)
			{
				case ElementType.Null:
					return null;

				case ElementType.Array:
					var elementType = ReadType();

					objectID = ReadInt32();

					int dimensions = ReadInt32();

					var lowerBounds = new int[dimensions];
					var lengths = new int[dimensions];

					for (int i = 0; i < dimensions; i++)
					{
						lowerBounds[i] = ReadInt32();

						lengths[i] = ReadInt32();
					}

					var array = Array.CreateInstance(elementType, lengths, lowerBounds);

					AssociateObjectIDToValue(objectID, array);

					VisitArrayElements(array, (arr, indices) => arr.SetValue(ReadObject(), indices));

					return array;

				case ElementType.Instance:
					var type = ReadType();

					object value;

					if (type.IsEnum)
					{
						var enumUnderlyingType = type.GetEnumUnderlyingType();

						if (enumUnderlyingType == typeof(Byte))
						{
							value = Enum.ToObject(type, ReadByte());
						}
						else if (enumUnderlyingType == typeof(Int16))
						{
							value = Enum.ToObject(type, ReadInt16());
						}
						else if (enumUnderlyingType == typeof(Int32))
						{
							value = Enum.ToObject(type, ReadInt32());
						}
						else if (enumUnderlyingType == typeof(Int64))
						{
							value = Enum.ToObject(type, ReadInt64());
						}
						else if (enumUnderlyingType == typeof(SByte))
						{
							value = Enum.ToObject(type, ReadSByte());
						}
						else if (enumUnderlyingType == typeof(UInt16))
						{
							value = Enum.ToObject(type, ReadUInt16());
						}
						else if (enumUnderlyingType == typeof(UInt32))
						{
							value = Enum.ToObject(type, ReadUInt32());
						}
						else if (enumUnderlyingType == typeof(UInt64))
						{
							value = Enum.ToObject(type, ReadUInt64());
						}
						else
						{
							throw new SerializationException(
								String.Format("Unsupported underlying type '{0}' of enum type '{1}'.", enumUnderlyingType.FullName, type.FullName));
						}
					}
					else if (type == typeof(Boolean))
					{
						value = ReadBoolean();
					}
					else if (type == typeof(Byte))
					{
						value = ReadByte();
					}
					else if (type == typeof(Char))
					{
						value = ReadChar();
					}
					else if (type == typeof(DateTime))
					{
						value = ReadDateTime();
					}
					else if (type == typeof(Decimal))
					{
						value = ReadDecimal();
					}
					else if (type == typeof(Double))
					{
						value = ReadDouble();
					}
					else if (type == typeof(Int16))
					{
						value = ReadInt16();
					}
					else if (type == typeof(Int32))
					{
						value = ReadInt32();
					}
					else if (type == typeof(Int64))
					{
						value = ReadInt64();
					}
					else if (type == typeof(SByte))
					{
						value = ReadSByte();
					}
					else if (type == typeof(Single))
					{
						value = ReadSingle();
					}
					else if (type == typeof(TimeSpan))
					{
						value = ReadTimeSpan();
					}
					else if (type == typeof(UInt16))
					{
						value = ReadUInt16();
					}
					else if (type == typeof(UInt32))
					{
						value = ReadUInt32();
					}
					else if (type == typeof(UInt64))
					{
						value = ReadUInt64();
					}
					else
					{
						if (!type.IsValueType)
						{
							objectID = ReadInt32();
						}
						else
						{
							objectID = -1;
						}

						if (type == typeof(String))
						{
							value = ReadString();

							AssociateObjectIDToValue(objectID, value);
						}
						else
						{
							var surrogate = GetSurrogateFor(type);

							if (surrogate != null)
							{
								var serializationInfo = ReadSerializationInfo(type);

								value = FormatterServices.GetUninitializedObject(type);

								if (objectID != -1)
								{
									AssociateObjectIDToValue(objectID, value);

									PushUreferencedObjectIDDuringSurrogation(objectID);
								}

								var surrogateValue = surrogate.SetObjectData(value, serializationInfo, context, surrogateSelector);

								if (surrogateValue != null && surrogateValue != value)
								{
									value = surrogateValue;

									if (objectID != -1)
									{
										if (!PopUreferencedObjectDuringSurrogation(objectID))
										{
											throw new SerializationException(
												String.Format("Deserialization of '{0}' using surrogate '{1}' resulted in cyclical reference " +
												"while the surrogate's SetObjectData returned a different object than the supplied one. " +
												"This is the unsupported scenario of FastBinaryFormatter.",
												type.FullName,
												surrogate.GetType().FullName));
										}

										AssociateObjectIDToValue(objectID, value);
									}
								}
								else if (objectID != -1)
								{
									PopUreferencedObjectDuringSurrogation(objectID);
								}

								FireSerializationEvent(value, typeof(OnDeserializedAttribute));
							}
							else if (typeof(IObjectReference).IsAssignableFrom(type))
							{
								var serializationInfo = ReadSerializationInfo(type);

								if (objectID != -1)
								{
									PushUreferencedObjectIDDuringSurrogation(objectID);
								}

								var objectReference = GetObjectReferenceForType(type, serializationInfo);

								if (objectID != -1)
								{
									if (!PopUreferencedObjectDuringSurrogation(objectID))
									{
										throw new SerializationException(
											String.Format("Deserialization using IObjectReference implementation '{1}' resulted in cyclical reference." +
											"This is the unsupported scenario of FastBinaryFormatter.",
											type.FullName));
									}

									AssociateObjectIDToValue(objectID, objectReference);
								}

								value = objectReference.GetRealObject(context);

								FireSerializationEvent(value, typeof(OnDeserializedAttribute));
							}
							else if (typeof(ISerializable).IsAssignableFrom(type))
							{
								var serializationConstructor = GetDeserializationConstructor(type);

								var serializationInfo = ReadSerializationInfo(type);

								value = FormatterServices.GetUninitializedObject(type);

								if (objectID != -1) AssociateObjectIDToValue(objectID, value);

								FireSerializationEvent(value, typeof(OnDeserializingAttribute));

								serializationConstructor.Invoke(value, new object[] { serializationInfo, context });

								FireSerializationEvent(value, typeof(OnDeserializedAttribute));
							}
							else
							{
								value = FormatterServices.GetUninitializedObject(type);

								if (objectID != -1) AssociateObjectIDToValue(objectID, value);

								FireSerializationEvent(value, typeof(OnDeserializingAttribute));

								int fieldsCount = ReadInt32();

								var fieldsDictionary = new Dictionary<string, object>(fieldsCount);

								for (int i = 0; i < fieldsCount; i++)
								{
									string fieldName = ReadString();

									object fieldValue = ReadObject();

									fieldsDictionary[fieldName] = fieldValue;
								}

								MemberInfo[] serializableMembers = GetSerializableMembersViaReflection(type);

								object[] serializableMemberValues = new object[serializableMembers.Length];

								for (int i = 0; i < serializableMembers.Length; i++)
								{
									var member = serializableMembers[i];

									object fieldValue;

									if (fieldsDictionary.TryGetValue(member.Name, out fieldValue))
									{
										serializableMemberValues[i] = fieldValue;
									}
									else
									{
										if (!IsOptional(member))
										{
											throw new SerializationException(String.Format("The stream does not contain a value for field name '{0}'.", member.Name));
										}
									}
								}

								FormatterServices.PopulateObjectMembers(value, serializableMembers, serializableMemberValues);

								FireSerializationEvent(value, typeof(OnDeserializedAttribute));
							}

							var deserializationCallbackImplementation = value as IDeserializationCallback;

							if (deserializationCallbackImplementation != null)
							{
								deserializationCallbacksList.Add(deserializationCallbackImplementation);
							}

						}

					}

					return value;

				case ElementType.ObjectRef:
					objectID = ReadInt32();

					if (objectID >= idsToObjectsList.Count)
						throw new SerializationException("A reference to an object ID has been encountered which has not been defined.");

					value = idsToObjectsList[objectID];

					if (PopUreferencedObjectDuringSurrogation(objectID))
					{
						return null;
					}

					var objectReferenceImplementation = value as IObjectReference;

					if (objectReferenceImplementation != null) value = objectReferenceImplementation.GetRealObject(context);

					return value;

				default:
					throw new SerializationException(String.Format("Unexpected element in deserialization stream: {0}", elementInStream));
			}
		}

		/// <summary>
		/// Returns true if a member has been marked with <see cref="OptionalFieldAttribute"/>.
		/// </summary>
		private bool IsOptional(MemberInfo member)
		{
			bool isOptional;

			if (!memberInfoOptionalityCache.TryGetValue(member, out isOptional))
			{
				isOptional = member.GetCustomAttributes(typeof(OptionalFieldAttribute), false).Length > 0;

				memberInfoOptionalityCache[member] = isOptional;
			}

			return isOptional;
		}

		/// <summary>
		/// Get the constructor having signature (<see cref="SerializationInfo"/>, <see cref="StreamingContext"/>) or
		/// throw a <see cref="SerializationException"/> if no such constructor exists for a type.
		/// </summary>
		/// <param name="type">The type being searched for the particular constructor.</param>
		/// <returns>
		/// Returns the constructor or throws a <see cref="SerializationException"/> if no such constructor exists.
		/// </returns>
		/// <exception cref="SerializationException">When no such constructor exists.</exception>
		private ConstructorInfo GetDeserializationConstructor(Type type)
		{
			ConstructorInfo deserializationConstructor;

			if (!this.typesToDeserializationConstructors.TryGetValue(type, out deserializationConstructor))
			{
				deserializationConstructor = type.GetConstructor(
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
					null,
					new Type[] { typeof(SerializationInfo), typeof(StreamingContext) },
					null);

				if (deserializationConstructor == null)
				{
					throw new SerializationException(
						String.Format(
							"The type '{0}' implementing ISerializable doesn't contain a serialization constructor " +
							"with parameters SerializationInfo, StreamingContext.",
							type.FullName
						)
					);
				}

				this.typesToDeserializationConstructors[type] = deserializationConstructor;
			}

			return deserializationConstructor;
		}

		/// <summary>
		/// Get an instance for a type deriving from <see cref="IObjectReference"/>.
		/// </summary>
		/// <param name="objectReferenceType">The type deriving from <see cref="IObjectReference"/>.</param>
		/// <param name="serializationInfo">
		/// The serialization info read from the stream. Used when the <paramref name="objectReferenceType"/>
		/// also implements <see cref="ISerializable"/>.
		/// </param>
		/// <returns>
		/// Returns the instance as <see cref="IObjectReference"/>.
		/// </returns>
		/// <exception cref="SerializationException">
		/// When the <paramref name="objectReferenceType"/> doesn't derive from <see cref="IObjectReference"/>
		/// or doesn't have a default constructor.
		/// </exception>
		private IObjectReference GetObjectReferenceForType(Type objectReferenceType, SerializationInfo serializationInfo)
		{
			if (typeof(IObjectReference).IsAssignableFrom(objectReferenceType))
			{
				throw new SerializationException(
					String.Format("The type '{0}' is not an IObjectReference implementation.", objectReferenceType.FullName));
			}

			if (typeof(ISerializable).IsAssignableFrom(objectReferenceType))
			{
				var serializationConstructor = GetDeserializationConstructor(objectReferenceType);

				return (IObjectReference)serializationConstructor.Invoke(new object[] { serializationInfo, context });
			}

			return (IObjectReference)CreateViaDefaultConstructor(objectReferenceType);
		}

		/// <summary>
		/// Gets the default constructor of a type (public or not) if such exists, else throws a <see cref="SerializationException"/>.
		/// </summary>
		/// <param name="type">The type having the constructor.</param>
		/// <returns>
		/// Returns the constructor found, else throws a <see cref="SerializationException"/>.
		/// </returns>
		/// <exception cref="SerializationException">
		/// When the type has no default constructor.
		/// </exception>
		private ConstructorInfo GetDefaultConstructor(Type type)
		{
			ConstructorInfo defaultConstructor;

			if (!this.typesToDefaultConstructors.TryGetValue(type, out defaultConstructor))
			{
				defaultConstructor = type.GetConstructor(
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
					null,
					Type.EmptyTypes,
					null);

				if (defaultConstructor == null)
				{
					throw new SerializationException(
						String.Format("The type '{0}' does not offer a default constructor.", type.FullName));
				}

				this.typesToDefaultConstructors[type] = defaultConstructor;
			}

			return defaultConstructor;
		}

		/// <summary>
		/// Create an instance via the default constructor of a type (public or not) 
		/// if such exists, else throws a <see cref="SerializationException"/>.
		/// </summary>
		/// <param name="type">The type of the instance.</param>
		/// <returns>
		/// Returns the created instance, else throws a <see cref="SerializationException"/>.
		/// </returns>
		/// <exception cref="SerializationException">
		/// When the type has no default constructor.
		/// </exception>
		private object CreateViaDefaultConstructor(Type type)
		{
			var defaultConstructor = GetDefaultConstructor(type);

			return defaultConstructor.Invoke(emptyParameters);
		}

		/// <summary>
		/// Read a <see cref="SerializationInfo"/> from the current position of the stream.
		/// </summary>
		/// <param name="type">The type holding the fields to be deserialized.</param>
		/// <returns>Returns the info populated with field names and values.</returns>
		/// <remarks>
		/// The stream must point to: fields count, (Field name, value)*.
		/// </remarks>
		private SerializationInfo ReadSerializationInfo(Type type)
		{
			var info = new SerializationInfo(type, converter);

			int fieldsCount = ReadInt32();

			for (int i = 0; i < fieldsCount; i++)
			{
				string fieldName = ReadString();

				object fieldValue = ReadObject();

				Type fieldType = (fieldValue != null) ? fieldValue.GetType() : typeof(object);

				info.AddValue(fieldName, fieldValue, fieldType);
			}

			return info;
		}

		/// <summary>
		/// Associate a value with an object ID.
		/// </summary>
		/// <param name="objectID">The object ID of the value.</param>
		/// <param name="value">The value.</param>
		private void AssociateObjectIDToValue(int objectID, object value)
		{
			AccommodateList(idsToObjectsList, objectID);

			idsToObjectsList[objectID] = value;
		}

		/// <summary>
		/// Reads a <see cref="ElementType.Type"/> or <see cref="ElementType.TypeRef"/> element depending on
		/// what follows in the stream.
		/// </summary>
		/// <remarks>
		/// Takes into account the <see cref="SerializationBinder"/> associated with the formatter.
		/// </remarks>
		/// <exception cref="SerializationException">
		/// When the next element in the stream is not <see cref="ElementType.Type"/> or <see cref="ElementType.TypeRef"/>,
		/// or
		/// when the type or at least one of its ancestors is not marked as serializable.
		/// </exception>
		private Type ReadType()
		{
			var elementType = (ElementType)ReadByte();

			int typeID = ReadInt32();

			try
			{

				switch (elementType)
				{
					case ElementType.TypeRef:
						return idsToTypesList[typeID];

					case ElementType.Type:
						AccommodateList(idsToTypesList, typeID);

						string assemblyName = ReadString();
						string typeName = ReadString();

						var type = binder.BindToType(assemblyName, typeName);

						EnsureTypeIsSerializable(type);

						idsToTypesList[typeID] = type;

						return type;

					default:
						throw new SerializationException("The stream is not properly structured. Expecting Type or TypeRef");
				}
			}
			catch (IndexOutOfRangeException ex)
			{
				throw new SerializationException("A reference to a type ID appeared before the type was defined.", ex);
			}
		}

		/// <summary>
		/// Read a requested number of bytes from the stream. 
		/// Throws a <see cref="EndOfStreamException"/> if the stream can't provide the requested number of bytes.
		/// </summary>
		/// <param name="buffer">The buffer to accept the read bytes.</param>
		/// <param name="offset">The offset in the <paramref name="buffer"/> where bytes are placed.</param>
		/// <param name="length">The amount of bytes requested from the stream.</param>
		/// <exception cref="EndOfStreamException">Whren the stream can't provide the requested number of bytes.</exception>
		private void ExpectFromStream(byte[] buffer, int offset, int length)
		{
			int bytesRead = stream.Read(buffer, offset, length);

			if (bytesRead != length)
			{
				throw new EndOfStreamException("The stream ended prematurely.");
			}
		}

		/// <summary>
		/// Read a byte from the stream.
		/// Throws a <see cref="EndOfStreamException"/> if the stream can't provide the requested byte.
		/// </summary>
		/// <returns>Returns the byte.</returns>
		/// <exception cref="EndOfStreamException">Whren the stream can't provide the byte.</exception>
		private byte ExpectByteFromStream()
		{
			int value = stream.ReadByte();

			if (value == -1) throw new EndOfStreamException("The stream ended prematurely.");

			return (byte)value;
		}

		/// <summary>
		/// Adds an object ID to the set of yet unreferenced surrogate objects.
		/// </summary>
		/// <param name="objectID">The object ID.</param>
		private void PushUreferencedObjectIDDuringSurrogation(int objectID)
		{
			unusedObjectIDsDuringSurrogation.Add(objectID);
		}

		/// <summary>
		/// Attempts to find and remove an object ID from the set of yet unreferenced surrogate objects.
		/// </summary>
		/// <param name="objectID">The object ID.</param>
		/// <returns>Returns true if the object was found, else false if the ID was referenced.</returns>
		private bool PopUreferencedObjectDuringSurrogation(int objectID)
		{
			return unusedObjectIDsDuringSurrogation.Remove(objectID);
		}

		#endregion
	}
}
