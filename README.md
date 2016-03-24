# Gramma.Serialization
This library,  [also available via Nuget](https://www.nuget.org/packages/Gramma.Serialization/), provides the `FastBinaryFormatter` class, an [`IFormatter`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.iformatter(v=vs.100).aspx) implementation as a replacement for the standard  BinaryFormatter for serialization purposes. It has the following features:

1. It has a higher upper limit for the number of objects being serialized. The standard BinaryFormatter  [has a limit of ~13.2 million objects](https://connect.microsoft.com/VisualStudio/feedback/details/303278/binary-serialization-fails-for-moderately-large-object-graphs). The FastBinaryFormatter limits 2^31 reference-type instances and poses no limits to value-type instances. 
2. It runs faster and is less memory-demanding, especially during deserialization. 
3. Serialization streams typically have smaller size compared to the ones produced by BinaryFormatter and they are not compatible. 
4. Serialization streams are portable between 32 bit and 64 bit applications. 
5. Relies on the standards. As a consequence, existing serializable classes, like those in the .NET base class library, need no change. Specifically, it supports: 
  * [`SerializableAttribute`](http://msdn.microsoft.com/en-us/library/system.serializableattribute(v=vs.100).aspx)
  * [`NonSerializedAttribute`](http://msdn.microsoft.com/en-us/library/system.nonserializedattribute(v=vs.100).aspx)
  * [`OptionalFieldAttribute`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.optionalfieldattribute(v=vs.100).aspx) 
  * [`OnSerializingAttribute`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.onserializingattribute(v=vs.100).aspx)
  * [`OnSerializedAttribute`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.onserializedattribute(v=vs.100).aspx)
  * [`OnDeserializingAttribute`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.ondeserializingattribute(v=vs.100).aspx)
  * [`OnDeserializedAttribute`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.ondeserializedattribute(v=vs.100).aspx)
  * [`ISerializable`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.iserializable(v=vs.100).aspx)
  * [`IDeserializationCallback`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.ideserializationcallback(v=vs.100).aspx)
  * [`IObjectReference`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.iobjectreference(v=vs.100).aspx)
  * [`ISerializationSurrogate`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.iserializationsurrogate(v=vs.100).aspx)

The significant performance gain comes at the cost of not supporting a very specific scenario:

>During deserialization, if a registered [`ISerializationSurrogate`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.iserializationsurrogate(v=vs.100).aspx) returns 
in method [`SetObjectData`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.iserializationsurrogate.setobjectdata(v=vs.100).aspx) a (non-null) different object 
than the one supplied to populate, then the data supplied 
in the [`SerializationInfo`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.serializationinfo(v=vs.100).aspx) must not recursively cause 
a deserialization cycle up to the new object. 
A cycle is allowed though if it is established outside the deserialized objects. 
In particular, cycles formed by associations created by other surrogates are allowed.

>The same restriction applies to 
[`IObjectReference`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.iobjectreference(v=vs.100).aspx) implementations.

>Note that support for returning a different non-null object in method 
[`SetObjectData`](http://msdn.microsoft.com/en-us/library/system.runtime.serialization.iserializationsurrogate.setobjectdata(v=vs.100).aspx)
has effect in version .NET 2.0 and later. The return value was previously ignored.

This scenario is highly unlikely to be found in practice. However, it is detected during deserialization and an exception is thrown upon occurrence.

The library has no dependencies.

##Example usage
Using `FastBinaryFormatter` is same as built-in .NET serialization formatters.

```cs
Album[] serializedObject = BuildAlbumArray(); // Example object graph.

var formatter = new FastBinaryFormatter();

// If required, define surragate selectors.
var surrogateSelector = new SurrogateSelector();

surrogateSelector.AddSurrogate(typeof(Genre), new StreamingContext(), new GenreSerializationSurrogate());

formatter.SurrogateSelector.ChainSelector(surrogateSelector);

using (var stream = new MemoryStream())
{
	// Serialize.
	formatter.Serialize(stream, serializedObject);

	stream.Seek(0, SeekOrigin.Begin);

	// Deserialize.
	var deserializedObject = (Album[])formatter.Deserialize(stream);
}
```
