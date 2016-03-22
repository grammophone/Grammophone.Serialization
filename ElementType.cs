using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Serialization
{
	/// <summary>
	/// Descriptor byte of following bytes in the binary stream. All ID values are of type <see cref="Int32"/>.
	/// </summary>
	public enum ElementType : byte
	{
		/// <summary>
		/// Null reference.
		/// </summary>
		Null,

		/// <summary>
		/// Follows: Type ID, Assembly name, Type name.
		/// </summary>
		Type,

		/// <summary>
		/// Follows: Type ID.
		/// </summary>
		TypeRef,

		/// <summary>
		/// Follows: element [Type | TypeRef], object id, dimensions count, dimension 0 (lower bound, length), ..., dimension n-1 bounds, [Instance | ObjectRef]*.
		/// </summary>
		Array,

		/// <summary>
		/// Follows: [Type | TypeRef], (object ID)?, [(fields count, (Field name, value)*) | primitive].
		/// </summary>
		/// <remarks>
		/// Object ID is not written if the instance is value type.
		/// </remarks>
		Instance,

		/// <summary>
		/// Follows: object ID.
		/// </summary>
		ObjectRef
	}
}
