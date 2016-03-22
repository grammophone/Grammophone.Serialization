using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Reflection;
using System.IO;

namespace Gramma.Serialization
{
	/// <summary>
	/// An <see cref="IFormatter"/> implementation for serializing objects in a custom binary format.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The stream format is a series of bytes prepended by <see cref="ElementType"/> descriptor bytes.
	/// See the <see cref="ElementType"/> members documentation for detailed structure.
	/// </para>
	/// <para>
	/// This formatter implements almost all serialization infrastructure with one exception:
	/// </para>
	/// <para>
	/// If a registered serialization surrogate returns in method <see cref="ISerializationSurrogate.SetObjectData"/>
	/// a (non-null) different object than the one supplied
	/// to populate, then the data supplied in the <see cref="SerializationInfo"/> 
	/// must not recursively cause a serialization cycle up to the new object.
	/// A cycle is allowed though if it is established outside the de/serialized objects. 
	/// In particular, cycles formed by associations created by other surrogates are allowed.
	/// </para>
	/// <para>
	/// The same restriction applies to <see cref="IObjectReference"/> implementations. 
	/// </para>
	/// <para>
	/// Note that support for returning a different non-null object in method <see cref="ISerializationSurrogate.SetObjectData"/> has
	/// effect in version .NET 2.0 and later. The return value was previously ignored.
	/// </para>
	/// </remarks>
	public class FastBinaryFormatter : IFormatter
	{
		#region Private fields

		private SerializationBinder binder;

		private StreamingContext context;

		private ISurrogateSelector surrogateSelector;

		private IFormatterConverter converter;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FastBinaryFormatter()
		{
			binder = new DefaultSerializationBinder();
			context = new StreamingContext();
			surrogateSelector = new SurrogateSelector();

			converter = new FormatterConverter();

			this.InternFieldNames = true;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The type converter to be used when implementations of 
		/// <see cref="ISerializable"/> and <see cref="ISerializationSurrogate"/> 
		/// pass values through <see cref="SerializationInfo"/>.
		/// Default is <see cref="FormatterConverter"/>.
		/// </summary>
		public IFormatterConverter Converter
		{
			get
			{
				return converter;
			}
			set
			{
				if (value == null) throw new ArgumentNullException("value");

				converter = value;
			}
		}

		/// <summary>
		/// If true, serialization will intern the field names of all objects.
		/// This is irrelevant to deserialization, as it will be automatically
		/// detected whether field interning was used or when the stream was produced.
		/// Default is true.
		/// </summary>
		public bool InternFieldNames { get; private set; }

		#endregion

		#region IFormatter Members

		/// <summary>
		/// The converter between types and string representations.
		/// Default is <see cref="DefaultSerializationBinder"/>.
		/// </summary>
		public SerializationBinder Binder
		{
			get
			{
				return binder;
			}
			set
			{
				if (value == null) throw new ArgumentNullException("value");

				binder = value;
			}
		}

		/// <summary>
		/// The streaming context for formatter.
		/// </summary>
		public StreamingContext Context
		{
			get
			{
				return context;
			}
			set
			{
				context = value;
			}
		}

		/// <summary>
		/// Deserializes the data on the provided stream and reconstitutes the graph of objects.
		/// </summary>
		/// <param name="serializationStream">The stream that contains the data to deserialize.</param>
		/// <returns>The top object of the deserialized graph.</returns>
		public object Deserialize(System.IO.Stream serializationStream)
		{
			var objectSerializer = new ObjectReader(serializationStream, this);

			return objectSerializer.Read();
		}

		/// <summary>
		/// Serializes an object, or graph of objects with the given root to the provided stream.
		/// </summary>
		/// <param name="serializationStream">The stream where the formatter puts the serialized data.</param>
		/// <param name="graph">The object, or root of the object graph, to serialize.</param>
		public void Serialize(System.IO.Stream serializationStream, object graph)
		{
			var objectSerializer = new ObjectWriter(serializationStream, this);

			objectSerializer.Write(graph);
		}

		/// <summary>
		/// The surrogate selector defining the serialization overrides.
		/// </summary>
		public ISurrogateSelector SurrogateSelector
		{
			get
			{
				return surrogateSelector;
			}
			set
			{
				if (surrogateSelector == null) throw new ArgumentNullException("surrogateSelector");
				
				surrogateSelector = value;
			}
		}

		#endregion
	}
}
