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
	/// Common base for <see cref="ObjectReader"/> and <see cref="ObjectWriter"/>.
	/// Holds formatter state.
	/// </summary>
	internal abstract class ObjectStreamer
	{
		#region Auxilliary types

		/// <summary>
		/// Used as a key for searching methods of a type which are marked with an attribute.
		/// </summary>
		private struct AttributedMethodSearch
		{
			public Type HolderType;
			public Type AttributeType;

			public override int GetHashCode()
			{
				return 23 * this.AttributeType.GetHashCode() + HolderType.GetHashCode();
			}
		}

		#endregion

		#region Private fields

		/// <summary>
		/// The serialization binder as specified by the formatter.
		/// </summary>
		/// <remarks>
		/// This is provided by the formatter parameter in the constructor.
		/// </remarks>
		protected SerializationBinder binder;

		/// <summary>
		/// The streaming context as specified by the formatter.
		/// </summary>
		/// <remarks>
		/// This is provided by the formatter parameter in the constructor.
		/// </remarks>
		protected StreamingContext context;

		/// <summary>
		/// The surrogate selector of the formatter.
		/// </summary>
		/// <remarks>
		/// This is provided by the formatter parameter in the constructor.
		/// </remarks>
		protected ISurrogateSelector surrogateSelector;

		/// <summary>
		/// The stream used for serializing or deserializing objects.
		/// </summary>
		protected Stream stream;

		/// <summary>
		/// The type converter as specified by the formatter.
		/// </summary>
		/// <remarks>
		/// This is provided by the formatter parameter in the constructor.
		/// </remarks>
		protected IFormatterConverter converter;

		/// <summary>
		/// An empty object array commonly used for invoking parameterless methods or constructors
		/// using reflection.
		/// </summary>
		protected object[] emptyParameters;

		/// <summary>
		/// Maps types to serializable members via reflection, 
		/// excluding those marked with <see cref="NonSerializedAttribute"/>.
		/// </summary>
		private Dictionary<Type, MemberInfo[]> serializableMembersByType;

		/// <summary>
		/// Holds a dictionary of atributed methods keyed by holder type and attribute type.
		/// </summary>
		private Dictionary<AttributedMethodSearch, IList<MethodInfo>> attributedMethodsDictionary;

		/// <summary>
		/// A cache holding surrogates of types. If a type does not have a surrogate, 
		/// the corresponding entry's value is null.
		/// </summary>
		private Dictionary<Type, ISerializationSurrogate> surrogatesByType;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="stream">The stream to read or write.</param>
		/// <param name="formatter">The formatter holding the serialization properties.</param>
		public ObjectStreamer(Stream stream, FastBinaryFormatter formatter)
		{
			if (stream == null) throw new ArgumentNullException("stream");
			if (formatter == null) throw new ArgumentNullException("formatter");

			this.stream = stream;
			this.binder = formatter.Binder;
			this.context = formatter.Context;
			this.surrogateSelector = formatter.SurrogateSelector;
			this.converter = formatter.Converter;

			this.emptyParameters = new object[0];

			this.serializableMembersByType = new Dictionary<Type, MemberInfo[]>();
			this.attributedMethodsDictionary = new Dictionary<AttributedMethodSearch, IList<MethodInfo>>();
			this.surrogatesByType = new Dictionary<Type, ISerializationSurrogate>();
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Make a list large enough to accommodate <paramref name="lastIndex"/>, padding with the 
		/// default objects of type <typeparamref name="T"/> as necessary.
		/// </summary>
		/// <typeparam name="T">The type of items in the list.</typeparam>
		/// <param name="list">The list.</param>
		/// <param name="lastIndex">The desired minimum last index of the list.</param>
		protected static void AccommodateList<T>(List<T> list, int lastIndex)
		{
			if (list == null) throw new ArgumentNullException("list");

			for (int i = list.Count; i <= lastIndex; i++)
			{
				list.Add(default(T));
			}
		}

		/// <summary>
		/// Visit each array element and perform an action.
		/// </summary>
		/// <param name="array">The array whose elements to visit.</param>
		/// <param name="action">The action to perform as a function of the array and the element index.</param>
		protected static void VisitArrayElements(Array array, Action<Array, int[]> action)
		{
			if (array == null) throw new ArgumentNullException("array");
			if (action == null) throw new ArgumentNullException("action");

			int dimensions = array.Rank;

			int[] indices = new int[dimensions];

			for (int i = 0; i < indices.Length; i++)
			{
				var lowerBound = array.GetLowerBound(i);
				var upperBound = array.GetUpperBound(i);

				if (lowerBound > upperBound) return;

				indices[i] = lowerBound;
			}

			bool nextCombinationExists;

			do
			{
				action(array, indices);

				nextCombinationExists = false;

				for (int currentDimension = 0; currentDimension < dimensions; currentDimension++)
				{
					int currentIndex = indices[currentDimension];

					int currentUpperBound = array.GetUpperBound(currentDimension);

					if (++currentIndex > currentUpperBound)
					{
						indices[currentDimension] = array.GetLowerBound(currentDimension);
					}
					else
					{
						indices[currentDimension] = currentIndex;
						nextCombinationExists = true;
						break;
					}
				}

			}
			while (nextCombinationExists);

		}

		/// <summary>
		/// Get the serialization surrogate for a type, if any, else return null.
		/// </summary>
		/// <param name="type">The type to check for surrogate.</param>
		/// <returns>
		/// Returns the associated surrogate, if any, else null.
		/// </returns>
		protected ISerializationSurrogate GetSurrogateFor(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			if (surrogateSelector == null) return null;

			ISerializationSurrogate surrogate;

			if (!surrogatesByType.TryGetValue(type, out surrogate))
			{
				ISurrogateSelector selectorFoundInChain;

				surrogate = surrogateSelector.GetSurrogate(type, context, out selectorFoundInChain);

				surrogatesByType[type] = surrogate;
			}

			return surrogate;
		}

		/// <summary>
		/// Calls any methods which are marked with a given attribute and accepting a single parameter 
		/// of type <see cref="StreamingContext"/>.
		/// </summary>
		/// <param name="value">The object to search for methods.</param>
		/// <param name="attributeType">The type of the attribute.</param>
		protected void FireSerializationEvent(object value, Type attributeType)
		{
			if (attributeType == null) throw new ArgumentNullException("attributeType");

			if (value == null) return;

			var type = value.GetType();

			IList<MethodInfo> onSerializingMethods = GetMethodsWithAttribute(type, attributeType);

			for (int i = 0; i < onSerializingMethods.Count; i++)
			{
				var onSerializingMethod = onSerializingMethods[i];

				onSerializingMethod.Invoke(value, new object[] { context });
			}
		}

		/// <summary>
		/// Returns the methods of a type which are tagged with a given attribute and taking a 
		/// single parameter of type <see cref="StreamingContext"/>.
		/// </summary>
		/// <param name="type">The type having the methods.</param>
		/// <param name="attributeType">The type of the attribute.</param>
		/// <returns>Returns public and non-public methods conforming to the above search criteria.</returns>
		protected IList<MethodInfo> GetMethodsWithAttribute(Type type, Type attributeType)
		{
			if (type == null) throw new ArgumentNullException("type");
			if (attributeType == null) throw new ArgumentNullException("attributeType");

			var searchKey = new AttributedMethodSearch { HolderType = type, AttributeType = attributeType };

			IList<MethodInfo> attributedMethods;

			if (!attributedMethodsDictionary.TryGetValue(searchKey, out attributedMethods))
			{

				var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				attributedMethods = new List<MethodInfo>(methods.Length);

				for (int i = 0; i < methods.Length; i++)
				{
					var method = methods[i];

					var attributes = method.GetCustomAttributes(attributeType, true);

					if (attributes.Length > 0)
					{
						var parameters = method.GetParameters();

						if (parameters.Length != 1 || parameters[0].ParameterType != typeof(StreamingContext))
						{
							throw new SerializationException(
								String.Format(
								"The method '{0}' attributed with '{1}' expects a single SerializationContext parameter.",
								method.Name,
								attributeType.Name));
						}

						attributedMethods.Add(method);
					}
				}

				attributedMethodsDictionary.Add(searchKey, attributedMethods);

			}

			return attributedMethods;
		}

		/// <summary>
		/// Ensures that a type and all its base types are marked with the <see cref="SerializableAttribute"/>.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <exception cref="SerializationException">
		/// When the type or at least one of its ancestors is not marked as serializable.
		/// </exception>
		protected void EnsureTypeIsSerializable(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			for (; type != null; type = type.BaseType)
			{
				if (!type.IsEnum && !type.IsInterface && type.GetCustomAttributes(typeof(SerializableAttribute), false).Length == 0)
				{
					throw new SerializationException(String.Format("The type '{0}' is not marked with SerializableAttribute.", type.FullName));
				}
			}
		}

		/// <summary>
		/// Get the serializable members of a type via reflection, 
		/// excluding those marked with <see cref="NonSerializedAttribute"/>.
		/// </summary>
		/// <param name="type">The type whose members to examine.</param>
		protected MemberInfo[] GetSerializableMembersViaReflection(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			MemberInfo[] members;

			if (!serializableMembersByType.TryGetValue(type, out members))
			{
				members = FormatterServices.GetSerializableMembers(type, context);

				serializableMembersByType[type] = members;
			}

			return members;
		}

		#endregion
	}
}
