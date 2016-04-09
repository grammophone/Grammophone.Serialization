using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Text;

namespace Grammophone.Serialization
{
	/// <summary>
	/// Default SerializationBinder implementation. Attempts to load the desired type with no restrictions.
	/// </summary>
	public class DefaultSerializationBinder : SerializationBinder
	{
		/// <summary>
		/// Create.
		/// </summary>
		public DefaultSerializationBinder()
		{
			this.AssemblyStyle = FormatterAssemblyStyle.Full;
		}

		/// <summary>
		/// Attempts to load a type via <see cref="Type.GetType(String)"/>.
		/// </summary>
		/// <param name="assemblyName">The assembly name of the type.</param>
		/// <param name="typeName">The namespace qualified type name.</param>
		/// <returns>Returns the type found or null.</returns>
		/// <remarks>
		/// The method supports both fully qualified assembly names including version and signature or simple names.
		/// </remarks>
		public override Type BindToType(string assemblyName, string typeName)
		{
			if (assemblyName == null) throw new ArgumentNullException("assemblyName");
			if (typeName == null) throw new ArgumentNullException("typeName");

			string qualifiedTypeName = String.Format("{0}, {1}", typeName, assemblyName);

			switch (this.AssemblyStyle)
			{
				case FormatterAssemblyStyle.Full:
					return Type.GetType(qualifiedTypeName, true);
				
				case FormatterAssemblyStyle.Simple:
					return Type.GetType(qualifiedTypeName, assemblyID => Assembly.LoadWithPartialName(assemblyID.Name), null, true);
				
				default:
					throw new SerializationException(
						String.Format("Unsupported AssemblyStyle '{0}' defined in DefaultSerializationBinder.", this.AssemblyStyle));
			}

		}

		/// <summary>
		/// Get the assembly name and type name of a type.
		/// The assembly name style is controlled via <see cref="AssemblyStyle"/> property.
		/// </summary>
		/// <param name="serializedType">The type from which to extract assembly and type names.</param>
		/// <param name="assemblyName">The name of the assembly in form specified by <see cref="AssemblyStyle"/> property.</param>
		/// <param name="typeName">The namespace-qualified type name.</param>
		public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
		{
			if (serializedType == null) throw new ArgumentNullException("serializedType");

			assemblyName = serializedType.Assembly.FullName;

			switch (this.AssemblyStyle)
			{
				case FormatterAssemblyStyle.Full:
					assemblyName = serializedType.Assembly.FullName;
					break;
				
				case FormatterAssemblyStyle.Simple:
					assemblyName = serializedType.Assembly.GetName().Name;
					break;

				default:
					throw new SerializationException("Unsupported AssemblyStyle.");
			}

			typeName = serializedType.FullName;
		}

		/// <summary>
		/// Controls the assembly name output in method <see cref="BindToName"/>.
		/// Default is <see cref="FormatterAssemblyStyle.Full"/>.
		/// </summary>
		public FormatterAssemblyStyle AssemblyStyle { get; set; }
	}
}
