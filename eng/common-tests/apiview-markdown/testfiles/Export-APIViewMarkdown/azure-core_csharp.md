```c#
Dependencies: 
System.ClientModel-1.7.0 

namespace Azure { 
    // <summary> 
    // A collection of values that may take multiple service requests to 
    // iterate over. 
    // </summary> 
    // <typeparam name="T">The type of the values.</typeparam> 
    // <example> 
    // Example of enumerating an AsyncPageable using the <c> async foreach </c> loop: 
    // <code snippet="Snippet:AsyncPageable" language="csharp"> 
    // // call a service method, which returns AsyncPageable&lt;T&gt; 
    // AsyncPageable&lt;SecretProperties&gt; allSecretProperties = client.GetPropertiesOfSecretsAsync(); 
    //  
    // await foreach (SecretProperties secretProperties in allSecretProperties) 
    // { 
    // Console.WriteLine(secretProperties.Name); 
    // } 
    // </code> 
    // or using a while loop: 
    // <code snippet="Snippet:AsyncPageableLoop" language="csharp"> 
    // // call a service method, which returns AsyncPageable&lt;T&gt; 
    // AsyncPageable&lt;SecretProperties&gt; allSecretProperties = client.GetPropertiesOfSecretsAsync(); 
    //  
    // IAsyncEnumerator&lt;SecretProperties&gt; enumerator = allSecretProperties.GetAsyncEnumerator(); 
    // try 
    // { 
    // while (await enumerator.MoveNextAsync()) 
    // { 
    // SecretProperties secretProperties = enumerator.Current; 
    // Console.WriteLine(secretProperties.Name); 
    // } 
    // } 
    // finally 
    // { 
    // await enumerator.DisposeAsync(); 
    // } 
    // </code> 
    // </example> 
    public abstract class AsyncPageable<T> where T : notnull : IAsyncEnumerable<T> where T : notnull { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.AsyncPageable`1" /> 
        // class for mocking. 
        // </summary> 
        protected AsyncPageable(); 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.AsyncPageable`1" /> 
        // class. 
        // </summary> 
        // <param name="cancellationToken"> 
        // The <see cref="P:Azure.AsyncPageable`1.CancellationToken" /> used for requests made while 
        // enumerating asynchronously. 
        // </param> 
        protected AsyncPageable(CancellationToken cancellationToken); 
        // <summary> 
        // Gets a <see cref="P:Azure.AsyncPageable`1.CancellationToken" /> used for requests made while 
        // enumerating asynchronously. 
        // </summary> 
        protected virtual CancellationToken CancellationToken { get; }
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Pageable`1" /> using the provided pages. 
        // </summary> 
        // <param name="pages">The pages of values to list as part of net new pageable instance.</param> 
        // <returns>A new instance of <see cref="T:Azure.Pageable`1" /></returns> 
        public static AsyncPageable<T> FromPages(IEnumerable<Page<T>> pages); 
        // <summary> 
        // Enumerate the values a <see cref="T:Azure.Page`1" /> at a time.  This may 
        // make multiple service requests. 
        // </summary> 
        // <param name="continuationToken"> 
        // A continuation token indicating where to resume paging or null to 
        // begin paging from the beginning. 
        // </param> 
        // <param name="pageSizeHint"> 
        // The number of items per <see cref="T:Azure.Page`1" /> that should be requested (from 
        // service operations that support it). It's not guaranteed that the value will be respected. 
        // </param> 
        // <returns> 
        // An async sequence of <see cref="T:Azure.Page`1" />s. 
        // </returns> 
        public abstract IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null); 
        // <summary> 
        // Enumerate the values in the collection asynchronously.  This may 
        // make multiple service requests. 
        // </summary> 
        // <param name="cancellationToken"> 
        // The <see cref="P:Azure.AsyncPageable`1.CancellationToken" /> used for requests made while 
        // enumerating asynchronously. 
        // </param> 
        // <returns>An async sequence of values.</returns> 
        public virtual IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default); 
        // <summary> 
        // Check if two <see cref="T:Azure.AsyncPageable`1" /> instances are equal. 
        // </summary> 
        // <param name="obj">The instance to compare to.</param> 
        // <returns>True if they're equal, false otherwise.</returns> 
        public override bool Equals(object? obj); 
        // <summary> 
        // Get a hash code for the <see cref="T:Azure.AsyncPageable`1" />. 
        // </summary> 
        // <returns>Hash code for the <see cref="T:Azure.Page`1" />.</returns> 
        public override int GetHashCode(); 
        // <summary> 
        // Creates a string representation of an <see cref="T:Azure.AsyncPageable`1" />. 
        // </summary> 
        // <returns> 
        // A string representation of an <see cref="T:Azure.AsyncPageable`1" />. 
        // </returns> 
        public override string? ToString(); 
    } 

    // <summary> 
    // Extensions that can be used for serialization. 
    // </summary> 
    public static class AzureCoreExtensions { 
        // <summary> 
        // Return the content of the BinaryData as a dynamic type.  Please see https://aka.ms/azsdk/net/dynamiccontent for details. 
        // </summary> 
        public static dynamic ToDynamicFromJson(this BinaryData utf8Json); 
        // <summary> 
        // Return the content of the BinaryData as a dynamic type.  Please see https://aka.ms/azsdk/net/dynamiccontent for details. 
        // <paramref name="propertyNameFormat">The format of property names in the JSON content. 
        // This value indicates to the dynamic type that it can convert property names on the returned value to this format in the underlying JSON. 
        // Please see https://aka.ms/azsdk/net/dynamiccontent#use-c-naming-conventions for details. 
        // </paramref> 
        // <paramref name="dateTimeFormat">The standard format specifier to pass when serializing DateTime and DateTimeOffset values in the JSON content. 
        // To serialize to unix time, pass the value <code>"x"</code> and 
        // see <see href="https://learn.microsoft.com/dotnet/standard/base-types/standard-date-and-time-format-strings">https://learn.microsoft.com/dotnet/standard/base-types/standard-date-and-time-format-strings#table-of-format-specifiers</see> for other well known values. 
        // </paramref> 
        // </summary> 
        public static dynamic ToDynamicFromJson(this BinaryData utf8Json, JsonPropertyNames propertyNameFormat, string dateTimeFormat = "o"); 
        // <summary> 
        // Converts the <see cref="T:System.BinaryData" /> to the specified type using 
        // the provided <see cref="T:Azure.Core.Serialization.ObjectSerializer" />. 
        // </summary> 
        // <typeparam name="T">The type that the data should be 
        // converted to.</typeparam> 
        // <param name="data">The <see cref="T:System.BinaryData" /> instance to convert.</param> 
        // <param name="serializer">The serializer to use 
        // when deserializing the data.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during deserialization.</param> 
        // <returns>The data converted to the specified type.</returns> 
        public static T? ToObject<T>(this BinaryData data, ObjectSerializer serializer, CancellationToken cancellationToken = default); 
        // <summary> 
        // Converts the <see cref="T:System.BinaryData" /> to the specified type using 
        // the provided <see cref="T:Azure.Core.Serialization.ObjectSerializer" />. 
        // </summary> 
        // <typeparam name="T">The type that the data should be 
        // converted to.</typeparam> 
        // <param name="data">The <see cref="T:System.BinaryData" /> instance to convert.</param> 
        // <param name="serializer">The serializer to use 
        // when deserializing the data.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during deserialization.</param> 
        // <returns>The data converted to the specified type.</returns> 
        public static ValueTask<T?> ToObjectAsync<T>(this BinaryData data, ObjectSerializer serializer, CancellationToken cancellationToken = default); 
        // <summary> 
        // Converts the json value represented by <see cref="T:System.BinaryData" /> to an object of a specific type. 
        // </summary> 
        // <param name="data">The <see cref="T:System.BinaryData" /> instance to convert.</param> 
        // <returns> The object value of the json value. 
        // If the object contains a primitive type such as string, int, double, bool, or null literal, it returns that type. 
        // Otherwise, it returns either an object[] or Dictionary&lt;string, object&gt;. 
        // Each value in the key value pair or list will also be converted into a primitive or another complex type recursively. 
        // </returns> 
        public static object? ToObjectFromJson(this BinaryData data); 
    } 

    // <summary> 
    // Key credential used to authenticate to an Azure Service. 
    // It provides the ability to update the key without creating a new client. 
    // </summary> 
    public class AzureKeyCredential : ApiKeyCredential { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.AzureKeyCredential" /> class. 
        // </summary> 
        // <param name="key">Key to use to authenticate with the Azure service.</param> 
        // <exception cref="T:System.ArgumentNullException"> 
        // Thrown when the <paramref name="key" /> is null. 
        // </exception> 
        // <exception cref="T:System.ArgumentException"> 
        // Thrown when the <paramref name="key" /> is empty. 
        // </exception> 
        public AzureKeyCredential(string key); 
        // <summary> 
        // Key used to authenticate to an Azure service. 
        // </summary> 
        public string Key { get; }
    } 

    // <summary> 
    // Credential allowing a named key to be used for authenticating to an Azure Service. 
    // It provides the ability to update the key without creating a new client. 
    // </summary> 
    public class AzureNamedKeyCredential { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.AzureNamedKeyCredential" /> class. 
        // </summary> 
        // <param name="name">The name of the <paramref name="key" />.</param> 
        // <param name="key">The key to use for authenticating with the Azure service.</param> 
        // <exception cref="T:System.ArgumentNullException"> 
        // Thrown when the <paramref name="name" /> or <paramref name="key" /> is null. 
        // </exception> 
        // <exception cref="T:System.ArgumentException"> 
        // Thrown when the <paramref name="name" /> or <paramref name="key" /> is empty. 
        // </exception> 
        public AzureNamedKeyCredential(string name, string key); 
        // <summary> 
        // Name of the key used to authenticate to an Azure service. 
        // </summary> 
        public string Name { get; }
        // <summary> 
        // Allows deconstruction of the credential into the associated name and key as an atomic operation. 
        // </summary> 
        // <param name="name">The name of the <paramref name="key" />.</param> 
        // <param name="key">The key to use for authenticating with the Azure service.</param> 
        // <example> 
        // <code snippet="Snippet:AzureNamedKeyCredential_Deconstruct" language="csharp"> 
        // var credential = new AzureNamedKeyCredential("SomeName", "SomeKey"); 
        //  
        // (string name, string key) = credential; 
        // </code> 
        // </example> 
        // <seealso href="https://docs.microsoft.com/dotnet/csharp/deconstruct">Deconstructing tuples and other types</seealso> 
        public void Deconstruct(out string name, out string key); 
        // <summary> 
        // Updates the named key.  This is intended to be used when you've regenerated your 
        // service key and want to update long-lived clients. 
        // </summary> 
        // <param name="name">The name of the <paramref name="key" />.</param> 
        // <param name="key">The key to use for authenticating with the Azure service.</param> 
        // <exception cref="T:System.ArgumentNullException"> 
        // Thrown when the <paramref name="name" /> or <paramref name="key" /> is null. 
        // </exception> 
        // <exception cref="T:System.ArgumentException"> 
        // Thrown when the <paramref name="name" /> or <paramref name="key" /> is empty. 
        // </exception> 
        public void Update(string name, string key); 
    } 

    // <summary> 
    // Shared access signature credential used to authenticate to an Azure Service. 
    // It provides the ability to update the shared access signature without creating a new client. 
    // </summary> 
    public class AzureSasCredential { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.AzureSasCredential" /> class. 
        // </summary> 
        // <param name="signature">Shared access signature to use to authenticate with the Azure service.</param> 
        // <exception cref="T:System.ArgumentNullException"> 
        // Thrown when the <paramref name="signature" /> is null. 
        // </exception> 
        // <exception cref="T:System.ArgumentException"> 
        // Thrown when the <paramref name="signature" /> is empty. 
        // </exception> 
        public AzureSasCredential(string signature); 
        // <summary> 
        // Shared access signature used to authenticate to an Azure service. 
        // </summary> 
        public string Signature { get; }
        // <summary> 
        // Updates the shared access signature. 
        // This is intended to be used when you've regenerated your shared access signature 
        // and want to update long lived clients. 
        // </summary> 
        // <param name="signature">Shared access signature to authenticate the service against.</param> 
        // <exception cref="T:System.ArgumentNullException"> 
        // Thrown when the <paramref name="signature" /> is null. 
        // </exception> 
        // <exception cref="T:System.ArgumentException"> 
        // Thrown when the <paramref name="signature" /> is empty. 
        // </exception> 
        public void Update(string signature); 
    } 

    // <summary> 
    // ErrorOptions controls the behavior of an operation when an unexpected response status code is received. 
    // </summary> 
    [Flags] 
    public enum ErrorOptions { 
        // <summary> 
        // Indicates that an operation should throw an exception when the response indicates a failure. 
        // </summary> 
        Default = 0, 
        // <summary> 
        // Indicates that an operation should not throw an exception when the response indicates a failure. 
        // Callers should check the Response.IsError property instead of catching exceptions. 
        // </summary> 
        NoThrow = 1, 
    } 

    // <summary> 
    // Represents an HTTP ETag. 
    // </summary> 
    [JsonConverter(typeof(ETagConverter))] 
    public readonly struct ETag : IEquatable<ETag> { 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.ETag" />. 
        // </summary> 
        // <param name="etag">The string value of the ETag.</param> 
        public ETag(string etag); 
        // <summary> 
        // Instance of <see cref="T:Azure.ETag" /> with the value. <code>*</code> 
        // </summary> 
        public static readonly ETag All; 
        // <summary> 
        // Compares equality of two <see cref="T:Azure.ETag" /> instances. 
        // </summary> 
        // <param name="left">The <see cref="T:Azure.ETag" /> to compare.</param> 
        // <param name="right">The <see cref="T:Azure.ETag" /> to compare to.</param> 
        // <returns><c>true</c> if values of both ETags are equal, otherwise <c>false</c>.</returns> 
        public static bool operator ==(ETag left, ETag right); 
        // <summary> 
        // Compares inequality of two <see cref="T:Azure.ETag" /> instances. 
        // </summary> 
        // <param name="left">The <see cref="T:Azure.ETag" /> to compare.</param> 
        // <param name="right">The <see cref="T:Azure.ETag" /> to compare to.</param> 
        // <returns><c>true</c> if values of both ETags are not equal, otherwise <c>false</c>.</returns> 
        public static bool operator !=(ETag left, ETag right); 
        // <summary>Indicates whether the current object is equal to another object of the same type.</summary><param name="other">An object to compare with this object.</param><returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns> 
        public bool Equals(ETag other); 
        // <summary> 
        // Indicates whether the value of current <see cref="T:Azure.ETag" /> is equal to the provided string.</summary> 
        // <param name="other">An object to compare with this object.</param> 
        // <returns>true if the current object is equal to the <paramref name="other">other</paramref> parameter; otherwise, false.</returns> 
        public bool Equals(string? other); 
        // <summary> 
        // Returns the string representation of the <see cref="T:Azure.ETag" />. 
        // </summary> 
        // <param name="format">A format string. Valid values are "G" for standard format and "H" for header format.</param> 
        // <returns>The formatted string representation of this <see cref="T:Azure.ETag" />. This includes outer quotes and the W/ prefix in the case of weak ETags.</returns> 
        // <example> 
        // <code> 
        // ETag tag = ETag.Parse("\"sometag\""); 
        // Console.WriteLine(tag.ToString("G")); 
        // // Displays: sometag 
        // Console.WriteLine(tag.ToString("H")); 
        // // Displays: "sometag" 
        // </code> 
        // </example> 
        public string ToString(string format); 
        // <summary>Indicates whether this instance and a specified object are equal.</summary><param name="obj">The object to compare with the current instance. </param><returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns> 
        public override bool Equals(object? obj); 
        // <summary>Returns the hash code for this instance.</summary><returns>A 32-bit signed integer that is the hash code for this instance.</returns> 
        public override int GetHashCode(); 
        // <summary> 
        //  
        // </summary> 
        // <returns>The string representation of this <see cref="T:Azure.ETag" />.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Represents authentication information in Authorization, ProxyAuthorization, 
    // WWW-Authenticate, and Proxy-Authenticate header values. 
    // </summary> 
    public class HttpAuthorization { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.HttpAuthorization" /> class. 
        // </summary> 
        // <param name="scheme"> 
        // The scheme to use for authorization. 
        // </param> 
        // <param name="parameter"> 
        // The credentials containing the authentication information of the 
        // user agent for the resource being requested. 
        // </param> 
        public HttpAuthorization(string scheme, string parameter); 
        // <summary> 
        // Gets the credentials containing the authentication information of the 
        // user agent for the resource being requested. 
        // </summary> 
        public string Parameter { get; }
        // <summary> 
        // Gets the scheme to use for authorization. 
        // </summary> 
        public string Scheme { get; }
        // <summary> 
        // Returns a string that represents the current <see cref="T:Azure.HttpAuthorization" /> object. 
        // </summary> 
        public override string ToString(); 
    } 

    // <summary> 
    // Defines a range of bytes within an HTTP resource, starting at an offset and 
    // ending at offset+count-1 inclusively. 
    // </summary> 
    public readonly struct HttpRange : IEquatable<HttpRange> { 
        // <summary> 
        // Creates an instance of HttpRange. 
        // </summary> 
        // <param name="offset">The starting offset of the <see cref="T:Azure.HttpRange" />. Defaults to 0.</param> 
        // <param name="length">The length of the range. null means to the end.</param> 
        public HttpRange(long offset = 0, long? length = null); 
        // <summary> 
        // Gets the size of the <see cref="T:Azure.HttpRange" />.  null means the range 
        // extends all the way to the end. 
        // </summary> 
        public long? Length { get; }
        // <summary> 
        // Gets the starting offset of the <see cref="T:Azure.HttpRange" />. 
        // </summary> 
        public long Offset { get; }
        // <summary> 
        // Check if two <see cref="T:Azure.HttpRange" /> instances are equal. 
        // </summary> 
        // <param name="left">The first instance to compare.</param> 
        // <param name="right">The second instance to compare.</param> 
        // <returns>True if they're equal, false otherwise.</returns> 
        public static bool operator ==(HttpRange left, HttpRange right); 
        // <summary> 
        // Check if two <see cref="T:Azure.HttpRange" /> instances are not equal. 
        // </summary> 
        // <param name="left">The first instance to compare.</param> 
        // <param name="right">The second instance to compare.</param> 
        // <returns>True if they're not equal, false otherwise.</returns> 
        public static bool operator !=(HttpRange left, HttpRange right); 
        // <summary> 
        // Check if two <see cref="T:Azure.HttpRange" /> instances are equal. 
        // </summary> 
        // <param name="other">The instance to compare to.</param> 
        // <returns>True if they're equal, false otherwise.</returns> 
        public bool Equals(HttpRange other); 
        // <summary> 
        // Check if two <see cref="T:Azure.HttpRange" /> instances are equal. 
        // </summary> 
        // <param name="obj">The instance to compare to.</param> 
        // <returns>True if they're equal, false otherwise.</returns> 
        public override bool Equals(object? obj); 
        // <summary> 
        // Get a hash code for the <see cref="T:Azure.HttpRange" />. 
        // </summary> 
        // <returns>Hash code for the <see cref="T:Azure.HttpRange" />.</returns> 
        public override int GetHashCode(); 
        // <summary> 
        // Converts the specified range to a string. 
        // </summary> 
        // <returns>String representation of the range.</returns> 
        // <remarks>For more information, see https://docs.microsoft.com/en-us/rest/api/storageservices/specifying-the-range-header-for-file-service-operations. </remarks> 
        public override string ToString(); 
    } 

    // <summary> 
    // Represents a JSON Patch document. 
    // </summary> 
    public class JsonPatchDocument { 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.JsonPatchDocument" /> that uses <see cref="T:Azure.Core.Serialization.JsonObjectSerializer" /> as the default serializer. 
        // </summary> 
        public JsonPatchDocument(); 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.JsonPatchDocument" /> 
        // </summary> 
        // <param name="serializer">The <see cref="T:Azure.Core.Serialization.ObjectSerializer" /> instance to use for value serialization.</param> 
        public JsonPatchDocument(ObjectSerializer serializer); 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.JsonPatchDocument" /> 
        // </summary> 
        // <param name="rawDocument">The binary representation of JSON Patch document.</param> 
        public JsonPatchDocument(ReadOnlyMemory<byte> rawDocument); 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.JsonPatchDocument" /> using an existing UTF8-encoded JSON Patch document. 
        // </summary> 
        // <param name="rawDocument">The binary representation of JSON Patch document.</param> 
        // <param name="serializer">The <see cref="T:Azure.Core.Serialization.ObjectSerializer" /> instance to use for value serialization.</param> 
        public JsonPatchDocument(ReadOnlyMemory<byte> rawDocument, ObjectSerializer serializer); 
        // <summary> 
        // Appends an "add" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="path">The path to apply the addition to.</param> 
        // <param name="value">The value to add to the path.</param> 
        public void AppendAdd<T>(string path, T value); 
        // <summary> 
        // Appends an "add" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="path">The path to apply the addition to.</param> 
        // <param name="rawJsonValue">The raw JSON value to add to the path.</param> 
        public void AppendAddRaw(string path, string rawJsonValue); 
        // <summary> 
        // Appends a "copy" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="from">The path to copy from.</param> 
        // <param name="path">The path to copy to.</param> 
        public void AppendCopy(string from, string path); 
        // <summary> 
        // Appends a "move" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="from">The path to move from.</param> 
        // <param name="path">The path to move to.</param> 
        public void AppendMove(string from, string path); 
        // <summary> 
        // Appends a "remove" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="path">The path to remove.</param> 
        public void AppendRemove(string path); 
        // <summary> 
        // Appends a "replace" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="path">The path to replace.</param> 
        // <param name="value">The value to replace with.</param> 
        public void AppendReplace<T>(string path, T value); 
        // <summary> 
        // Appends a "replace" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="path">The path to replace.</param> 
        // <param name="rawJsonValue">The raw JSON value to replace with.</param> 
        public void AppendReplaceRaw(string path, string rawJsonValue); 
        // <summary> 
        // Appends a "test" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="path">The path to test.</param> 
        // <param name="value">The value to replace with.</param> 
        public void AppendTest<T>(string path, T value); 
        // <summary> 
        // Appends a "test" operation to this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <param name="path">The path to test.</param> 
        // <param name="rawJsonValue">The raw JSON value to test against.</param> 
        public void AppendTestRaw(string path, string rawJsonValue); 
        // <summary> 
        // Returns a UTF8-encoded representation of this <see cref="T:Azure.JsonPatchDocument" /> instance. 
        // </summary> 
        // <returns>The UTF8-encoded JSON.</returns> 
        public ReadOnlyMemory<byte> ToBytes(); 
        // <summary> 
        // Returns a formatted JSON string representation of this <see cref="T:Azure.JsonPatchDocument" />. 
        // </summary> 
        // <returns>A formatted JSON string representation of this <see cref="T:Azure.JsonPatchDocument" />.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Specifies HTTP options for conditional requests. 
    // </summary> 
    public class MatchConditions { 
        public MatchConditions(); 
        // <summary> 
        // Optionally limit requests to resources that have a matching ETag. 
        // </summary> 
        public ETag? IfMatch { get; set; }
        // <summary> 
        // Optionally limit requests to resources that do not match the ETag. 
        // </summary> 
        public ETag? IfNoneMatch { get; set; }
    } 

    // <summary> 
    // Represents a result of Azure operation. 
    // </summary> 
    // <typeparam name="T">The type of returned value.</typeparam> 
    public abstract class NullableResponse<T> { 
        protected NullableResponse(); 
        // <summary> 
        // Gets a value indicating whether the current instance has a valid value of its underlying type. 
        // </summary> 
        public abstract bool HasValue { get; }
        // <summary> 
        // Gets the value returned by the service. Accessing this property will throw if <see cref="P:Azure.NullableResponse`1.HasValue" /> is false. 
        // </summary> 
        public abstract T? Value { get; }
        // <summary> 
        // Returns the HTTP response returned by the service. 
        // </summary> 
        // <returns>The HTTP response returned by the service.</returns> 
        public abstract Response GetRawResponse(); 
        // <summary>Determines whether the specified object is equal to the current object.</summary><param name="obj">The object to compare with the current object. </param><returns>true if the specified object  is equal to the current object; otherwise, false.</returns> 
        public override bool Equals(object? obj); 
        // <summary>Serves as the default hash function. </summary><returns>A hash code for the current object.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns a string that represents the current object.</summary><returns>A string that represents the current object.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Represents a long-running operation. 
    // </summary> 
    public abstract class Operation { 
        protected Operation(); 
        // <summary> 
        // Returns true if the long-running operation completed. 
        // </summary> 
        public abstract bool HasCompleted { get; }
        // <summary> 
        // Gets an ID representing the operation that can be used to poll for 
        // the status of the long-running operation. 
        // There are cases that operation id is not available, we return "NOT_SET" for unavailable operation id. 
        // </summary> 
        public abstract string Id { get; }
        // <summary> 
        // Rehydrates an operation from a <see cref="T:Azure.Core.RehydrationToken" />. 
        // </summary> 
        // <param name="pipeline">The Http pipeline.</param> 
        // <param name="rehydrationToken">The rehydration token.</param> 
        // <param name="options">The client options.</param> 
        // <returns>The long-running operation.</returns> 
        public static Operation<T> Rehydrate<T>(HttpPipeline pipeline, RehydrationToken rehydrationToken, ClientOptions? options = null) where T : IPersistableModel<T>; 
        // <summary> 
        // Rehydrates an operation from a <see cref="T:Azure.Core.RehydrationToken" />. 
        // </summary> 
        // <param name="pipeline">The Http pipeline.</param> 
        // <param name="rehydrationToken">The rehydration token.</param> 
        // <param name="options">The client options.</param> 
        // <returns>The long-running operation.</returns> 
        public static Operation Rehydrate(HttpPipeline pipeline, RehydrationToken rehydrationToken, ClientOptions? options = null); 
        // <summary> 
        // Rehydrates an operation from a <see cref="T:Azure.Core.RehydrationToken" />. 
        // </summary> 
        // <param name="pipeline">The Http pipeline.</param> 
        // <param name="rehydrationToken">The rehydration token.</param> 
        // <param name="options">The client options.</param> 
        // <returns>The long-running operation.</returns> 
        public static Task<Operation<T>> RehydrateAsync<T>(HttpPipeline pipeline, RehydrationToken rehydrationToken, ClientOptions? options = null) where T : IPersistableModel<T>; 
        // <summary> 
        // Rehydrates an operation from a <see cref="T:Azure.Core.RehydrationToken" />. 
        // </summary> 
        // <param name="pipeline">The Http pipeline.</param> 
        // <param name="rehydrationToken">The rehydration token.</param> 
        // <param name="options">The client options.</param> 
        // <returns>The long-running operation.</returns> 
        public static Task<Operation> RehydrateAsync(HttpPipeline pipeline, RehydrationToken rehydrationToken, ClientOptions? options = null); 
        // <summary> 
        // The last HTTP response received from the server. 
        // </summary> 
        // <remarks> 
        // The last response returned from the server during the lifecycle of this instance. 
        // An instance of <see cref="T:Azure.Operation`1" /> sends requests to a server in UpdateStatusAsync, UpdateStatus, and other methods. 
        // Responses from these requests can be accessed using GetRawResponse. 
        // </remarks> 
        public abstract Response GetRawResponse(); 
        // <summary> 
        // Get a token that can be used to rehydrate the operation. 
        // </summary> 
        public virtual RehydrationToken? GetRehydrationToken(); 
        // <summary> 
        // Calls the server to get updated status of the long-running operation. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the service call.</param> 
        // <returns>The HTTP response received from the server.</returns> 
        // <remarks> 
        // This operation will update the value returned from GetRawResponse and might update HasCompleted. 
        // </remarks> 
        public abstract Response UpdateStatus(CancellationToken cancellationToken = default); 
        // <summary> 
        // Calls the server to get updated status of the long-running operation. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the service call.</param> 
        // <returns>The HTTP response received from the server.</returns> 
        // <remarks> 
        // This operation will update the value returned from GetRawResponse and might update HasCompleted. 
        // </remarks> 
        public abstract ValueTask<Response> UpdateStatusAsync(CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final response of the operation. 
        // </remarks> 
        public virtual Response WaitForCompletionResponse(CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="pollingInterval"> 
        // The interval between status requests to the server. 
        // The interval can change based on information returned from the server. 
        // For example, the server might communicate to the client that there is not reason to poll for status change sooner than some time. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final response of the operation. 
        // </remarks> 
        public virtual Response WaitForCompletionResponse(TimeSpan pollingInterval, CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="delayStrategy"> 
        // The strategy to use to determine the delay between status requests to the server. If the server returns retry-after header, 
        // the delay used will be the maximum specified by the strategy and the header value. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final response of the operation. 
        // </remarks> 
        public virtual Response WaitForCompletionResponse(DelayStrategy delayStrategy, CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final response of the operation. 
        // </remarks> 
        public virtual ValueTask<Response> WaitForCompletionResponseAsync(CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="pollingInterval"> 
        // The interval between status requests to the server. 
        // The interval can change based on information returned from the server. 
        // For example, the server might communicate to the client that there is not reason to poll for status change sooner than some time. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final response of the operation. 
        // </remarks> 
        public virtual ValueTask<Response> WaitForCompletionResponseAsync(TimeSpan pollingInterval, CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="delayStrategy"> 
        // The strategy to use to determine the delay between status requests to the server. If the server returns retry-after header, 
        // the delay used will be the maximum specified by the strategy and the header value. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final response of the operation. 
        // </remarks> 
        public virtual ValueTask<Response> WaitForCompletionResponseAsync(DelayStrategy delayStrategy, CancellationToken cancellationToken = default); 
        // <summary>Determines whether the specified object is equal to the current object.</summary><param name="obj">The object to compare with the current object. </param><returns>true if the specified object  is equal to the current object; otherwise, false.</returns> 
        public override bool Equals(object? obj); 
        // <summary>Serves as the default hash function. </summary><returns>A hash code for the current object.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns a string that represents the current object.</summary><returns>A string that represents the current object.</returns> 
        public override string? ToString(); 
    } 

    // <summary> 
    // Represents a long-running operation that returns a value when it completes. 
    // </summary> 
    // <typeparam name="T">The final result of the long-running operation.</typeparam> 
    public abstract class Operation<T> where T : notnull : Operation { 
        protected Operation(); 
        // <summary> 
        // Returns true if the long-running operation completed successfully and has produced final result (accessible by Value property). 
        // </summary> 
        public abstract bool HasValue { get; }
        // <summary> 
        // Final result of the long-running operation. 
        // </summary> 
        // <remarks> 
        // This property can be accessed only after the operation completes successfully (HasValue is true). 
        // </remarks> 
        public abstract T Value { get; }
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final result of the operation. 
        // </remarks> 
        public virtual Response<T> WaitForCompletion(CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="pollingInterval"> 
        // The interval between status requests to the server. 
        // The interval can change based on information returned from the server. 
        // For example, the server might communicate to the client that there is not reason to poll for status change sooner than some time. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final result of the operation. 
        // </remarks> 
        public virtual Response<T> WaitForCompletion(TimeSpan pollingInterval, CancellationToken cancellationToken); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="delayStrategy"> 
        // The strategy to use to determine the delay between status requests to the server. If the server returns retry-after header, 
        // the delay used will be the maximum specified by the strategy and the header value. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final result of the operation. 
        // </remarks> 
        public virtual Response<T> WaitForCompletion(DelayStrategy delayStrategy, CancellationToken cancellationToken); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final result of the operation. 
        // </remarks> 
        public virtual ValueTask<Response<T>> WaitForCompletionAsync(CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="pollingInterval"> 
        // The interval between status requests to the server. 
        // The interval can change based on information returned from the server. 
        // For example, the server might communicate to the client that there is not reason to poll for status change sooner than some time. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final result of the operation. 
        // </remarks> 
        public virtual ValueTask<Response<T>> WaitForCompletionAsync(TimeSpan pollingInterval, CancellationToken cancellationToken); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="delayStrategy"> 
        // The strategy to use to determine the delay between status requests to the server. If the server returns retry-after header, 
        // the delay used will be the maximum specified by the strategy and the header value. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final result of the operation. 
        // </remarks> 
        public virtual ValueTask<Response<T>> WaitForCompletionAsync(DelayStrategy delayStrategy, CancellationToken cancellationToken); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final response of the operation. 
        // </remarks> 
        public override ValueTask<Response> WaitForCompletionResponseAsync(CancellationToken cancellationToken = default); 
        // <summary> 
        // Periodically calls the server till the long-running operation completes. 
        // </summary> 
        // <param name="pollingInterval"> 
        // The interval between status requests to the server. 
        // The interval can change based on information returned from the server. 
        // For example, the server might communicate to the client that there is not reason to poll for status change sooner than some time. 
        // </param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The last HTTP response received from the server.</returns> 
        // <remarks> 
        // This method will periodically call UpdateStatusAsync till HasCompleted is true, then return the final response of the operation. 
        // </remarks> 
        public override ValueTask<Response> WaitForCompletionResponseAsync(TimeSpan pollingInterval, CancellationToken cancellationToken = default); 
    } 

    // <summary> 
    // A single <see cref="T:Azure.Page`1" /> of values from a request that may return 
    // zero or more <see cref="T:Azure.Page`1" />s of values. 
    // </summary> 
    // <typeparam name="T">The type of values.</typeparam> 
    public abstract class Page<T> { 
        protected Page(); 
        // <summary> 
        // Gets the continuation token used to request the next 
        // <see cref="T:Azure.Page`1" />.  The continuation token may be null or 
        // empty when there are no more pages. 
        // </summary> 
        public abstract string? ContinuationToken { get; }
        // <summary> 
        // Gets the values in this <see cref="T:Azure.Page`1" />. 
        // </summary> 
        public abstract IReadOnlyList<T> Values { get; }
        // <summary> 
        // Creates a new <see cref="T:Azure.Page`1" />. 
        // </summary> 
        // <param name="values"> 
        // The values in this <see cref="T:Azure.Page`1" />. 
        // </param> 
        // <param name="continuationToken"> 
        // The continuation token used to request the next <see cref="T:Azure.Page`1" />. 
        // </param> 
        // <param name="response"> 
        // The <see cref="T:Azure.Response" /> that provided this <see cref="T:Azure.Page`1" />. 
        // </param> 
        public static Page<T> FromValues(IReadOnlyList<T> values, string? continuationToken, Response response); 
        // <summary> 
        // Gets the <see cref="T:Azure.Response" /> that provided this 
        // <see cref="T:Azure.Page`1" />. 
        // </summary> 
        public abstract Response GetRawResponse(); 
        // <summary> 
        // Check if two <see cref="T:Azure.Page`1" /> instances are equal. 
        // </summary> 
        // <param name="obj">The instance to compare to.</param> 
        // <returns>True if they're equal, false otherwise.</returns> 
        public override bool Equals(object? obj); 
        // <summary> 
        // Get a hash code for the <see cref="T:Azure.Page`1" />. 
        // </summary> 
        // <returns>Hash code for the <see cref="T:Azure.Page`1" />.</returns> 
        public override int GetHashCode(); 
        // <summary> 
        // Creates a string representation of an <see cref="T:Azure.Page`1" />. 
        // </summary> 
        // <returns> 
        // A string representation of an <see cref="T:Azure.Page`1" />. 
        // </returns> 
        public override string? ToString(); 
    } 

    // <summary> 
    // A collection of values that may take multiple service requests to 
    // iterate over. 
    // </summary> 
    // <typeparam name="T">The type of the values.</typeparam> 
    public abstract class Pageable<T> where T : notnull : IEnumerable<T> where T : notnull, IEnumerable { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.Pageable`1" /> 
        // class for mocking. 
        // </summary> 
        protected Pageable(); 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.Pageable`1" /> 
        // class. 
        // </summary> 
        // <param name="cancellationToken"> 
        // The <see cref="P:Azure.Pageable`1.CancellationToken" /> used for requests made while 
        // enumerating asynchronously. 
        // </param> 
        protected Pageable(CancellationToken cancellationToken); 
        // <summary> 
        // Gets a <see cref="P:Azure.Pageable`1.CancellationToken" /> used for requests made while 
        // enumerating asynchronously. 
        // </summary> 
        protected virtual CancellationToken CancellationToken { get; }
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Pageable`1" /> using the provided pages. 
        // </summary> 
        // <param name="pages">The pages of values to list as part of net new pageable instance.</param> 
        // <returns>A new instance of <see cref="T:Azure.Pageable`1" /></returns> 
        public static Pageable<T> FromPages(IEnumerable<Page<T>> pages); 
        // <summary> 
        // Enumerate the values a <see cref="T:Azure.Page`1" /> at a time.  This may 
        // make multiple service requests. 
        // </summary> 
        // <param name="continuationToken"> 
        // A continuation token indicating where to resume paging or null to 
        // begin paging from the beginning. 
        // </param> 
        // <param name="pageSizeHint"> 
        // The number of items per <see cref="T:Azure.Page`1" /> that should be requested (from 
        // service operations that support it). It's not guaranteed that the value will be respected. 
        // </param> 
        // <returns> 
        // An async sequence of <see cref="T:Azure.Page`1" />s. 
        // </returns> 
        public abstract IEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null); 
        // <summary> 
        // Enumerate the values in the collection.  This may make multiple service requests. 
        // </summary> 
        public virtual IEnumerator<T> GetEnumerator(); 
        // <summary>Returns an enumerator that iterates through a collection.</summary><returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns> 
        IEnumerator IEnumerable.GetEnumerator(); 
        // <summary> 
        // Check if two <see cref="T:Azure.Pageable`1" /> instances are equal. 
        // </summary> 
        // <param name="obj">The instance to compare to.</param> 
        // <returns>True if they're equal, false otherwise.</returns> 
        public override bool Equals(object? obj); 
        // <summary> 
        // Get a hash code for the <see cref="T:Azure.Pageable`1" />. 
        // </summary> 
        // <returns>Hash code for the <see cref="T:Azure.Pageable`1" />.</returns> 
        public override int GetHashCode(); 
        // <summary> 
        // Creates a string representation of an <see cref="T:Azure.Pageable`1" />. 
        // </summary> 
        // <returns> 
        // A string representation of an <see cref="T:Azure.Pageable`1" />. 
        // </returns> 
        public override string? ToString(); 
    } 

    // <summary> 
    // Represents a pageable long-running operation that exposes the results 
    // in either synchronous or asynchronous format. 
    // </summary> 
    // <typeparam name="T"></typeparam> 
    public abstract class PageableOperation<T> where T : notnull : Operation<AsyncPageable<T>> { 
        protected PageableOperation(); 
        // <summary> 
        // Gets the final result of the long-running operation asynchronously. 
        // </summary> 
        // <remarks> 
        // This property can be accessed only after the operation completes successfully (HasValue is true). 
        // </remarks> 
        public override AsyncPageable<T> Value { get; }
        // <summary> 
        // Gets the final result of the long-running operation synchronously. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The final result of the long-running operation synchronously.</returns> 
        // <remarks> 
        // Operation must complete successfully (HasValue is true) for it to provide values. 
        // </remarks> 
        public abstract Pageable<T> GetValues(CancellationToken cancellationToken = default); 
        // <summary> 
        // Gets the final result of the long-running operation asynchronously. 
        // </summary> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used for the periodical service calls.</param> 
        // <returns>The final result of the long-running operation asynchronously.</returns> 
        // <remarks> 
        // Operation must complete successfully (HasValue is true) for it to provide values. 
        // </remarks> 
        public abstract AsyncPageable<T> GetValuesAsync(CancellationToken cancellationToken = default); 
    } 

    // <summary> 
    // Specifies HTTP options for conditional requests based on modification time. 
    // </summary> 
    public class RequestConditions : MatchConditions { 
        public RequestConditions(); 
        // <summary> 
        // Optionally limit requests to resources that have only been 
        // modified since this point in time. 
        // </summary> 
        public DateTimeOffset? IfModifiedSince { get; set; }
        // <summary> 
        // Optionally limit requests to resources that have remained 
        // unmodified. 
        // </summary> 
        public DateTimeOffset? IfUnmodifiedSince { get; set; }
    } 

    // <summary> 
    // Options that can be used to control the behavior of a request sent by a client. 
    // </summary> 
    public class RequestContext { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.RequestContext" /> class. 
        // </summary> 
        public RequestContext(); 
        // <summary> 
        // The token to check for cancellation. 
        // </summary> 
        public CancellationToken CancellationToken { get; set; }
        // <summary> 
        // Controls under what conditions the operation raises an exception if the underlying response indicates a failure. 
        // </summary> 
        public ErrorOptions ErrorOptions { get; set; }
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.RequestContext" /> class using the given <see cref="P:Azure.RequestContext.ErrorOptions" />. 
        // </summary> 
        // <param name="options"></param> 
        public static implicit operator RequestContext(ErrorOptions options); 
        // <summary> 
        // Customizes the <see cref="T:Azure.Core.ResponseClassifier" /> for this operation to change 
        // the default <see cref="T:Azure.Response" /> classification behavior so that it considers 
        // the passed-in status code to be an error or not, as specified. 
        // Status code classifiers are applied after all <see cref="T:Azure.Core.ResponseClassificationHandler" /> classifiers. 
        // This is useful for cases where you'd like to prevent specific response status codes from being treated as errors by 
        // logging and distributed tracing policies -- that is, if a response is not classified as an error, it will not appear as an error in 
        // logs or distributed traces. 
        // </summary> 
        // <param name="statusCode">The status code to customize classification for.</param> 
        // <param name="isError">Whether the passed-in status code should be classified as an error.</param> 
        // <exception cref="T:System.ArgumentOutOfRangeException">statusCode is not between 100 and 599 (inclusive).</exception> 
        // <exception cref="T:System.InvalidOperationException">If this method is called after the <see cref="T:Azure.RequestContext" /> has been 
        // used in a method call.</exception> 
        public void AddClassifier(int statusCode, bool isError); 
        // <summary> 
        // Customizes the <see cref="T:Azure.Core.ResponseClassifier" /> for this operation. 
        // Adding a <see cref="T:Azure.Core.ResponseClassificationHandler" /> changes the classification 
        // behavior so that it first tries to classify a response via the handler, and if 
        // the handler doesn't have an opinion, it instead uses the default classifier. 
        // Handlers are applied in order so the most recently added takes precedence. 
        // This is useful for cases where you'd like to prevent specific response status codes from being treated as errors by 
        // logging and distributed tracing policies -- that is, if a response is not classified as an error, it will not appear as an error in 
        // logs or distributed traces. 
        // </summary> 
        // <param name="classifier">The custom classifier.</param> 
        // <exception cref="T:System.InvalidOperationException">If this method is called after the <see cref="T:Azure.RequestContext" /> has been 
        // used in a method call.</exception> 
        public void AddClassifier(ResponseClassificationHandler classifier); 
        // <summary> 
        // Adds an <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> into the pipeline for the duration of this request. 
        // The position of policy in the pipeline is controlled by <paramref name="position" /> parameter. 
        // If you want the policy to execute once per client request use <see cref="F:Azure.Core.HttpPipelinePosition.PerCall" /> 
        // otherwise use <see cref="F:Azure.Core.HttpPipelinePosition.PerRetry" /> to run the policy for every retry. 
        // </summary> 
        // <param name="policy">The <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> instance to be added to the pipeline.</param> 
        // <param name="position">The position of the policy in the pipeline.</param> 
        public void AddPolicy(HttpPipelinePolicy policy, HttpPipelinePosition position); 
    } 

    // <summary> 
    // An exception thrown when service request fails. 
    // </summary> 
    public class RequestFailedException : Exception, ISerializable { 
        // <summary>Initializes a new instance of the <see cref="T:Azure.RequestFailedException"></see> class with a specified error message.</summary> 
        // <param name="message">The message that describes the error.</param> 
        public RequestFailedException(string message); 
        // <summary>Initializes a new instance of the <see cref="T:Azure.RequestFailedException"></see> class with a specified error message, HTTP status code and a reference to the inner exception that is the cause of this exception.</summary> 
        // <param name="message">The error message that explains the reason for the exception.</param> 
        // <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param> 
        public RequestFailedException(string message, Exception? innerException); 
        // <summary>Initializes a new instance of the <see cref="T:Azure.RequestFailedException"></see> class with a specified error message and HTTP status code.</summary> 
        // <param name="status">The HTTP status code, or <c>0</c> if not available.</param> 
        // <param name="message">The message that describes the error.</param> 
        public RequestFailedException(int status, string message); 
        // <summary>Initializes a new instance of the <see cref="T:Azure.RequestFailedException"></see> class with a specified error message and a reference to the inner exception that is the cause of this exception.</summary> 
        // <param name="status">The HTTP status code, or <c>0</c> if not available.</param> 
        // <param name="message">The error message that explains the reason for the exception.</param> 
        // <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param> 
        public RequestFailedException(int status, string message, Exception? innerException); 
        // <summary>Initializes a new instance of the <see cref="T:Azure.RequestFailedException"></see> class with a specified error message, HTTP status code, error code, and a reference to the inner exception that is the cause of this exception.</summary> 
        // <param name="status">The HTTP status code, or <c>0</c> if not available.</param> 
        // <param name="message">The error message that explains the reason for the exception.</param> 
        // <param name="errorCode">The service specific error code.</param> 
        // <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param> 
        public RequestFailedException(int status, string message, string? errorCode, Exception? innerException); 
        // <summary>Initializes a new instance of the <see cref="T:Azure.RequestFailedException"></see> class 
        // with an error message, HTTP status code, and error code obtained from the specified response.</summary> 
        // <param name="response">The response to obtain error details from.</param> 
        public RequestFailedException(Response response); 
        // <summary>Initializes a new instance of the <see cref="T:Azure.RequestFailedException"></see> class 
        // with an error message, HTTP status code, and error code obtained from the specified response.</summary> 
        // <param name="response">The response to obtain error details from.</param> 
        // <param name="innerException">An inner exception to associate with the new <see cref="T:Azure.RequestFailedException" />.</param> 
        public RequestFailedException(Response response, Exception? innerException); 
        // <summary>Initializes a new instance of the <see cref="T:Azure.RequestFailedException"></see> class 
        // with an error message, HTTP status code, and error code obtained from the specified response.</summary> 
        // <param name="response">The response to obtain error details from.</param> 
        // <param name="innerException">An inner exception to associate with the new <see cref="T:Azure.RequestFailedException" />.</param> 
        // <param name="detailsParser">The parser to use to parse the response content.</param> 
        public RequestFailedException(Response response, Exception? innerException, RequestFailedDetailsParser? detailsParser); 
        // <summary>Initializes a new instance of the <see cref="T:System.Exception" /> class with serialized data.</summary><param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown. </param><param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination. </param><exception cref="T:System.ArgumentNullException">The <paramref name="info" /> parameter is null. </exception><exception cref="T:System.Runtime.Serialization.SerializationException">The class name is null or <see cref="P:System.Exception.HResult" /> is zero (0). </exception> 
        protected RequestFailedException(SerializationInfo info, StreamingContext context); 
        // <summary> 
        // Gets the service specific error code if available. Please refer to the client documentation for the list of supported error codes. 
        // </summary> 
        public string? ErrorCode { get; }
        // <summary> 
        // Gets the HTTP status code of the response. Returns. <code>0</code> if response was not received. 
        // </summary> 
        public int Status { get; }
        // <summary>When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with information about the exception.</summary><param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown. </param><param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination. </param><exception cref="T:System.ArgumentNullException">The <paramref name="info" /> parameter is a null reference (Nothing in Visual Basic). </exception> 
        public override void GetObjectData(SerializationInfo info, StreamingContext context); 
        // <summary> 
        // Gets the response, if any, that led to the exception. 
        // </summary> 
        public Response? GetRawResponse(); 
    } 

    // <summary> 
    // Represents the HTTP response from the service. 
    // </summary> 
    public abstract class Response : IDisposable { 
        protected Response(); 
        // <summary> 
        // Gets the client request id that was sent to the server as <c>x-ms-client-request-id</c> headers. 
        // </summary> 
        public abstract string ClientRequestId { get; set; }
        // <summary> 
        // Gets the contents of HTTP response, if it is available. 
        // </summary> 
        // <remarks> 
        // Throws <see cref="T:System.InvalidOperationException" /> when <see cref="P:Azure.Response.ContentStream" /> is not a <see cref="T:System.IO.MemoryStream" />. 
        // </remarks> 
        public virtual BinaryData Content { get; }
        // <summary> 
        // Gets the contents of HTTP response. Returns <c>null</c> for responses without content. 
        // </summary> 
        public abstract Stream? ContentStream { get; set; }
        // <summary> 
        // Get the HTTP response headers. 
        // </summary> 
        public virtual ResponseHeaders Headers { get; }
        // <summary> 
        // Indicates whether the status code of the returned response is considered 
        // an error code. 
        // </summary> 
        public virtual bool IsError { get; }
        // <summary> 
        // Gets the HTTP reason phrase. 
        // </summary> 
        public abstract string ReasonPhrase { get; }
        // <summary> 
        // Gets the HTTP status code. 
        // </summary> 
        public abstract int Status { get; }
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Response`1" /> with the provided value and HTTP response. 
        // </summary> 
        // <typeparam name="T">The type of the value.</typeparam> 
        // <param name="value">The value.</param> 
        // <param name="response">The HTTP response.</param> 
        // <returns>A new instance of <see cref="T:Azure.Response`1" /> with the provided value and HTTP response.</returns> 
        public static Response<T> FromValue<T>(T value, Response response); 
        // <summary> 
        // Frees resources held by this <see cref="T:Azure.Response" /> instance. 
        // </summary> 
        public abstract void Dispose(); 
        // <summary> 
        // Returns <c>true</c> if the header is stored in the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        protected abstract bool ContainsHeader(string name); 
        // <summary> 
        // Returns an iterator for enumerating <see cref="T:Azure.Core.HttpHeader" /> in the response. 
        // </summary> 
        // <returns>The <see cref="T:System.Collections.Generic.IEnumerable`1" /> enumerating <see cref="T:Azure.Core.HttpHeader" /> in the response.</returns> 
        protected abstract IEnumerable<HttpHeader> EnumerateHeaders(); 
        // <summary> 
        // Returns header value if the header is stored in the collection. If header has multiple values they are going to be joined with a comma. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The reference to populate with value.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        protected abstract bool TryGetHeader(string name, out string? value); 
        // <summary> 
        // Returns header values if the header is stored in the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="values">The reference to populate with values.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        protected abstract bool TryGetHeaderValues(string name, out IEnumerable<string>? values); 
        // <summary> 
        // Returns the string representation of this <see cref="T:Azure.Response" />. 
        // </summary> 
        // <returns>The string representation of this <see cref="T:Azure.Response" /></returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Represents a result of Azure operation. 
    // </summary> 
    // <typeparam name="T">The type of returned value.</typeparam> 
    [DebuggerTypeProxy(typeof(ResponseDebugView<>))] 
    public abstract class Response<T> : NullableResponse<T> { 
        protected Response(); 
        // <summary> 
        // Gets a value indicating whether the current instance has a valid value of its underlying type. 
        // </summary> 
        public override bool HasValue { get; }
        // <summary> 
        // Gets the value returned by the service. Accessing this property will throw if <see cref="P:Azure.NullableResponse`1.HasValue" /> is false. 
        // </summary> 
        public override T Value { get; }
        // <summary> 
        // Returns the value of this <see cref="T:Azure.Response`1" /> object. 
        // </summary> 
        // <param name="response">The <see cref="T:Azure.Response`1" /> instance.</param> 
        public static implicit operator T(Response<T> response); 
        // <summary>Determines whether the specified object is equal to the current object.</summary><param name="obj">The object to compare with the current object. </param><returns>true if the specified object  is equal to the current object; otherwise, false.</returns> 
        public override bool Equals(object? obj); 
        // <summary>Serves as the default hash function. </summary><returns>A hash code for the current object.</returns> 
        public override int GetHashCode(); 
    } 

    // <summary> 
    // Represents an error returned by an Azure Service. 
    // </summary> 
    // <summary> 
    // Represents an error returned by an Azure Service. 
    // </summary> 
    [JsonConverter(typeof(Converter))] 
    public sealed class ResponseError : IJsonModel<ResponseError>, IPersistableModel<ResponseError> { 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.ResponseError" />. 
        // </summary> 
        public ResponseError(); 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.ResponseError" />. 
        // </summary> 
        // <param name="code">The error code.</param> 
        // <param name="message">The error message.</param> 
        public ResponseError(string? code, string? message); 
        // <summary> 
        // Gets the error code. 
        // </summary> 
        public string? Code { get; }
        // <summary> 
        // Gets the error message. 
        // </summary> 
        public string? Message { get; }
        // <summary> 
        // Reads one JSON value (including objects or arrays) from the provided reader and converts it to a model. 
        // </summary><param name="reader">The <see cref="T:System.Text.Json.Utf8JsonReader" /> to read.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A <typeparamref name="T" /> representation of the JSON value.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        ResponseError IJsonModel<ResponseError>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options); 
        // <summary> 
        // Writes the model to the provided <see cref="T:System.Text.Json.Utf8JsonWriter" />. 
        // </summary><param name="writer">The <see cref="T:System.Text.Json.Utf8JsonWriter" /> to write into.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        void IJsonModel<ResponseError>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options); 
        // <summary> 
        // Converts the provided <see cref="T:System.BinaryData" /> into a model. 
        // </summary><param name="data">The <see cref="T:System.BinaryData" /> to parse.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A <typeparamref name="T" /> representation of the data.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        ResponseError IPersistableModel<ResponseError>.Create(BinaryData data, ModelReaderWriterOptions options); 
        // <summary> 
        // Gets the data interchange format (JSON, Xml, etc) that the model uses when communicating with the service. 
        // </summary><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to consider when serializing and deserializing the model.</param><returns>The format that the model uses when communicating with the service.</returns> 
        string IPersistableModel<ResponseError>.GetFormatFromOptions(ModelReaderWriterOptions options); 
        // <summary> 
        // Writes the model into a <see cref="T:System.BinaryData" />. 
        // </summary><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A binary representation of the written model.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        BinaryData IPersistableModel<ResponseError>.Write(ModelReaderWriterOptions options); 
        // <summary>Returns a string that represents the current object.</summary><returns>A string that represents the current object.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Provides data for <see cref="T:Azure.Core.SyncAsyncEventHandler`1" /> 
    // events that can be invoked either synchronously or asynchronously. 
    // </summary> 
    public class SyncAsyncEventArgs : EventArgs { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.SyncAsyncEventArgs" /> 
        // class. 
        // </summary> 
        // <param name="isRunningSynchronously"> 
        // A value indicating whether the event handler was invoked 
        // synchronously or asynchronously.  Please see 
        // <see cref="T:Azure.Core.SyncAsyncEventHandler`1" /> for more details. 
        // </param> 
        // <param name="cancellationToken"> 
        // A cancellation token related to the original operation that raised 
        // the event.  It's important for your handler to pass this token 
        // along to any asynchronous or long-running synchronous operations 
        // that take a token so cancellation will correctly propagate.  The 
        // default value is <see cref="P:System.Threading.CancellationToken.None" />. 
        // </param> 
        public SyncAsyncEventArgs(bool isRunningSynchronously, CancellationToken cancellationToken = default); 
        // <summary> 
        // Gets a cancellation token related to the original operation that 
        // raised the event.  It's important for your handler to pass this 
        // token along to any asynchronous or long-running synchronous 
        // operations that take a token so cancellation (via something like 
        // <code> 
        // new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token 
        // </code> 
        // for example) will correctly propagate. 
        // </summary> 
        public CancellationToken CancellationToken { get; }
        // <summary> 
        // Gets a value indicating whether the event handler was invoked 
        // synchronously or asynchronously.  Please see 
        // <see cref="T:Azure.Core.SyncAsyncEventHandler`1" /> for more details. 
        // </summary> 
        // <remarks> 
        // <para> 
        // The same <see cref="T:Azure.Core.SyncAsyncEventHandler`1" /> 
        // event can be raised from both synchronous and asynchronous code 
        // paths depending on whether you're calling sync or async methods on 
        // a client.  If you write an async handler but raise it from a sync 
        // method, the handler will be doing sync-over-async and may cause 
        // ThreadPool starvation.  See 
        // <see href="https://docs.microsoft.com/archive/blogs/vancem/diagnosing-net-core-threadpool-starvation-with-perfview-why-my-service-is-not-saturating-all-cores-or-seems-to-stall"> 
        // Diagnosing .NET Core ThreadPool Starvation with PerfView</see> for 
        // a detailed explanation of how that can cause ThreadPool starvation 
        // and serious performance problems. 
        // </para> 
        // <para> 
        // You can use this <see cref="P:Azure.SyncAsyncEventArgs.IsRunningSynchronously" /> property to check 
        // how the event is being raised and implement your handler 
        // accordingly.  Here's an example handler that's safe to invoke from 
        // both sync and async code paths. 
        // <code snippet="Snippet:Azure_Core_Samples_EventSamples_CombinedHandler" language="csharp"> 
        // var client = new AlarmClient(); 
        // client.Ring += async (SyncAsyncEventArgs e) =&gt; 
        // { 
        // if (e.IsRunningSynchronously) 
        // { 
        // Console.WriteLine("Wake up!"); 
        // } 
        // else 
        // { 
        // await Console.Out.WriteLineAsync("Wake up!"); 
        // } 
        // }; 
        //  
        // client.Snooze(); // sync call that blocks 
        // await client.SnoozeAsync(); // async call that doesn't block 
        // </code> 
        // </para> 
        // </remarks> 
        public bool IsRunningSynchronously { get; }
    } 

    // <summary> 
    // Indicates whether the invocation of a long running operation should return once it has 
    // started or wait for the server operation to fully complete before returning. 
    // </summary> 
    public enum WaitUntil { 
        // <summary> 
        // Indicates the method should wait until the server operation fully completes. 
        // </summary> 
        Completed = 0, 
        // <summary> 
        // Indicates the method should return once the server operation has started. 
        // </summary> 
        Started = 1, 
    } 

} 

namespace Azure.Core { 
    // <summary> 
    // Represents an Azure service bearer access token with expiry information. 
    // </summary> 
    public struct AccessToken { 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.AccessToken" /> using the provided <paramref name="accessToken" /> and <paramref name="expiresOn" />. 
        // </summary> 
        // <param name="accessToken">The bearer access token value.</param> 
        // <param name="expiresOn">The bearer access token expiry date.</param> 
        public AccessToken(string accessToken, DateTimeOffset expiresOn); 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.AccessToken" /> using the provided <paramref name="accessToken" /> and <paramref name="expiresOn" />. 
        // </summary> 
        // <param name="accessToken">The bearer access token value.</param> 
        // <param name="expiresOn">The bearer access token expiry date.</param> 
        // <param name="refreshOn">Specifies the time when the cached token should be proactively refreshed.</param> 
        public AccessToken(string accessToken, DateTimeOffset expiresOn, DateTimeOffset? refreshOn); 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.AccessToken" /> using the provided <paramref name="accessToken" /> and <paramref name="expiresOn" />. 
        // </summary> 
        // <param name="accessToken">The access token value.</param> 
        // <param name="expiresOn">The access token expiry date.</param> 
        // <param name="refreshOn">Specifies the time when the cached token should be proactively refreshed.</param> 
        // <param name="tokenType">The access token type.</param> 
        public AccessToken(string accessToken, DateTimeOffset expiresOn, DateTimeOffset? refreshOn, string tokenType); 
        // <summary> 
        // Gets the time when the provided token expires. 
        // </summary> 
        public DateTimeOffset ExpiresOn { get; }
        // <summary> 
        // Gets the time when the token should be refreshed. 
        // </summary> 
        public DateTimeOffset? RefreshOn { get; }
        // <summary> 
        // Get the access token value. 
        // </summary> 
        public string Token { get; }
        // <summary> 
        // Identifies the type of access token. 
        // </summary> 
        public string TokenType { get; }
        // <summary>Indicates whether this instance and a specified object are equal.</summary><param name="obj">The object to compare with the current instance. </param><returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns> 
        public override bool Equals(object? obj); 
        // <summary>Returns the hash code for this instance.</summary><returns>A 32-bit signed integer that is the hash code for this instance.</returns> 
        public override int GetHashCode(); 
    } 

    // <summary> 
    // Context class used by <see cref="T:System.ClientModel.Primitives.ModelReaderWriter" /> to read and write models in an AOT compatible way. 
    // </summary> 
    [ModelReaderWriterBuildable(typeof(ResponseError))] 
    [ModelReaderWriterBuildable(typeof(RehydrationToken))] 
    [ModelReaderWriterBuildable(typeof(ResponseInnerError))] 
    public class AzureCoreContext : ModelReaderWriterContext { 
        // <summary> Gets the default instance </summary> 
        public static AzureCoreContext Default { get; }
        // <summary> 
        // Tries to gets a <see cref="T:System.ClientModel.Primitives.ModelReaderWriterTypeBuilder" /> for the given <see cref="T:System.Type" /> to allow <see cref="T:System.ClientModel.Primitives.ModelReaderWriter" /> to work with AOT. 
        // </summary><param name="type">The type to get info for.</param><param name="builder">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterTypeBuilder" /> if found.</param><returns>True if the corresponding <see cref="T:System.ClientModel.Primitives.ModelReaderWriterTypeBuilder" /> if defined in the context otherwise false.</returns> 
        protected override bool TryGetTypeBuilderCore(Type type, out ModelReaderWriterTypeBuilder builder); 
    } 

    // <summary> 
    // Represents an Azure geography region where supported resource providers live. 
    // </summary> 
    public readonly struct AzureLocation : IEquatable<AzureLocation> { 
        // <summary> Initializes a new instance of Location. </summary> 
        // <param name="location"> The location name or the display name. </param> 
        public AzureLocation(string location); 
        // <summary> Initializes a new instance of Location. </summary> 
        // <param name="name"> The location name. </param> 
        // <param name="displayName"> The display name of the location. </param> 
        public AzureLocation(string name, string displayName); 
        // <summary> 
        // Public cloud location for Australia Central. 
        // </summary> 
        public static AzureLocation AustraliaCentral { get; }
        // <summary> 
        // Public cloud location for Australia Central 2. 
        // </summary> 
        public static AzureLocation AustraliaCentral2 { get; }
        // <summary> 
        // Public cloud location for Australia East. 
        // </summary> 
        public static AzureLocation AustraliaEast { get; }
        // <summary> 
        // Public cloud location for Australia Southeast. 
        // </summary> 
        public static AzureLocation AustraliaSoutheast { get; }
        // <summary> 
        // Public cloud location for Brazil South. 
        // </summary> 
        public static AzureLocation BrazilSouth { get; }
        // <summary> 
        // Public cloud location for Brazil Southeast. 
        // </summary> 
        public static AzureLocation BrazilSoutheast { get; }
        // <summary> 
        // Public cloud location for Canada Central. 
        // </summary> 
        public static AzureLocation CanadaCentral { get; }
        // <summary> 
        // Public cloud location for Canada East. 
        // </summary> 
        public static AzureLocation CanadaEast { get; }
        // <summary> 
        // Public cloud location for Central India. 
        // </summary> 
        public static AzureLocation CentralIndia { get; }
        // <summary> 
        // Public cloud location for Central US. 
        // </summary> 
        public static AzureLocation CentralUS { get; }
        // <summary> 
        // Public cloud location for China East. 
        // </summary> 
        public static AzureLocation ChinaEast { get; }
        // <summary> 
        // Public cloud location for China East 2. 
        // </summary> 
        public static AzureLocation ChinaEast2 { get; }
        // <summary> 
        // Public cloud location for China East 3. 
        // </summary> 
        public static AzureLocation ChinaEast3 { get; }
        // <summary> 
        // Public cloud location for China North. 
        // </summary> 
        public static AzureLocation ChinaNorth { get; }
        // <summary> 
        // Public cloud location for China North 2. 
        // </summary> 
        public static AzureLocation ChinaNorth2 { get; }
        // <summary> 
        // Public cloud location for China North 3. 
        // </summary> 
        public static AzureLocation ChinaNorth3 { get; }
        // <summary> 
        // Gets a location display name consisting of titlecase words or alphanumeric characters separated by whitespaces, e.g. "West US". 
        // </summary> 
        public string? DisplayName { get; }
        // <summary> 
        // Public cloud location for East Asia. 
        // </summary> 
        public static AzureLocation EastAsia { get; }
        // <summary> 
        // Public cloud location for East US. 
        // </summary> 
        public static AzureLocation EastUS { get; }
        // <summary> 
        // Public cloud location for East US 2. 
        // </summary> 
        public static AzureLocation EastUS2 { get; }
        // <summary> 
        // Public cloud location for France Central. 
        // </summary> 
        public static AzureLocation FranceCentral { get; }
        // <summary> 
        // Public cloud location for France South. 
        // </summary> 
        public static AzureLocation FranceSouth { get; }
        // <summary> 
        // Public cloud location for Germany Central. 
        // </summary> 
        public static AzureLocation GermanyCentral { get; }
        // <summary> 
        // Public cloud location for Germany North. 
        // </summary> 
        public static AzureLocation GermanyNorth { get; }
        // <summary> 
        // Public cloud location for Germany NorthEast. 
        // </summary> 
        public static AzureLocation GermanyNorthEast { get; }
        // <summary> 
        // Public cloud location for Germany West Central. 
        // </summary> 
        public static AzureLocation GermanyWestCentral { get; }
        // <summary> 
        // Public cloud location for Israel Central. 
        // </summary> 
        public static AzureLocation IsraelCentral { get; }
        // <summary> 
        // Public cloud location for Italy North. 
        // </summary> 
        public static AzureLocation ItalyNorth { get; }
        // <summary> 
        // Public cloud location for Japan East. 
        // </summary> 
        public static AzureLocation JapanEast { get; }
        // <summary> 
        // Public cloud location for Japan West. 
        // </summary> 
        public static AzureLocation JapanWest { get; }
        // <summary> 
        // Public cloud location for Korea Central. 
        // </summary> 
        public static AzureLocation KoreaCentral { get; }
        // <summary> 
        // Public cloud location for Korea South. 
        // </summary> 
        public static AzureLocation KoreaSouth { get; }
        // <summary> 
        // Public cloud location for Mexico Central. 
        // </summary> 
        public static AzureLocation MexicoCentral { get; }
        // <summary> 
        // Gets a location name consisting of only lowercase characters without white spaces or any separation character between words, e.g. "westus". 
        // </summary> 
        public string Name { get; }
        // <summary> 
        // Public cloud location for North Central US. 
        // </summary> 
        public static AzureLocation NorthCentralUS { get; }
        // <summary> 
        // Public cloud location for North Europe. 
        // </summary> 
        public static AzureLocation NorthEurope { get; }
        // <summary> 
        // Public cloud location for Norway East. 
        // </summary> 
        public static AzureLocation NorwayEast { get; }
        // <summary> 
        // Public cloud location for Norway West. 
        // </summary> 
        public static AzureLocation NorwayWest { get; }
        // <summary> 
        // Public cloud location for Poland Central. 
        // </summary> 
        public static AzureLocation PolandCentral { get; }
        // <summary> 
        // Public cloud location for Qatar Central. 
        // </summary> 
        public static AzureLocation QatarCentral { get; }
        // <summary> 
        // Public cloud location for South Africa North. 
        // </summary> 
        public static AzureLocation SouthAfricaNorth { get; }
        // <summary> 
        // Public cloud location for South Africa West. 
        // </summary> 
        public static AzureLocation SouthAfricaWest { get; }
        // <summary> 
        // Public cloud location for South Central US. 
        // </summary> 
        public static AzureLocation SouthCentralUS { get; }
        // <summary> 
        // Public cloud location for Southeast Asia. 
        // </summary> 
        public static AzureLocation SoutheastAsia { get; }
        // <summary> 
        // Public cloud location for South India. 
        // </summary> 
        public static AzureLocation SouthIndia { get; }
        // <summary> 
        // Public cloud location for Spain Central. 
        // </summary> 
        public static AzureLocation SpainCentral { get; }
        // <summary> 
        // Public cloud location for Sweden Central. 
        // </summary> 
        public static AzureLocation SwedenCentral { get; }
        // <summary> 
        // Public cloud location for Sweden South. 
        // </summary> 
        public static AzureLocation SwedenSouth { get; }
        // <summary> 
        // Public cloud location for Switzerland North. 
        // </summary> 
        public static AzureLocation SwitzerlandNorth { get; }
        // <summary> 
        // Public cloud location for Switzerland West. 
        // </summary> 
        public static AzureLocation SwitzerlandWest { get; }
        // <summary> 
        // Public cloud location for UAE Central. 
        // </summary> 
        public static AzureLocation UAECentral { get; }
        // <summary> 
        // Public cloud location for UAE North. 
        // </summary> 
        public static AzureLocation UAENorth { get; }
        // <summary> 
        // Public cloud location for UK South. 
        // </summary> 
        public static AzureLocation UKSouth { get; }
        // <summary> 
        // Public cloud location for UK West. 
        // </summary> 
        public static AzureLocation UKWest { get; }
        // <summary> 
        // Public cloud location for US DoD Central. 
        // </summary> 
        public static AzureLocation USDoDCentral { get; }
        // <summary> 
        // Public cloud location for US DoD East. 
        // </summary> 
        public static AzureLocation USDoDEast { get; }
        // <summary> 
        // Public cloud location for US Gov Arizona. 
        // </summary> 
        public static AzureLocation USGovArizona { get; }
        // <summary> 
        // Public cloud location for US Gov Iowa. 
        // </summary> 
        public static AzureLocation USGovIowa { get; }
        // <summary> 
        // Public cloud location for US Gov Texas. 
        // </summary> 
        public static AzureLocation USGovTexas { get; }
        // <summary> 
        // Public cloud location for US Gov Virginia. 
        // </summary> 
        public static AzureLocation USGovVirginia { get; }
        // <summary> 
        // Public cloud location for West Central US. 
        // </summary> 
        public static AzureLocation WestCentralUS { get; }
        // <summary> 
        // Public cloud location for West Europe. 
        // </summary> 
        public static AzureLocation WestEurope { get; }
        // <summary> 
        // Public cloud location for West India. 
        // </summary> 
        public static AzureLocation WestIndia { get; }
        // <summary> 
        // Public cloud location for West US. 
        // </summary> 
        public static AzureLocation WestUS { get; }
        // <summary> 
        // Public cloud location for West US 2. 
        // </summary> 
        public static AzureLocation WestUS2 { get; }
        // <summary> 
        // Public cloud location for West US 3. 
        // </summary> 
        public static AzureLocation WestUS3 { get; }
        // <summary> 
        // Compares this <see cref="T:Azure.Core.AzureLocation" /> instance with another object and determines if they are equals. 
        // </summary> 
        // <param name="left"> The object on the left side of the operator. </param> 
        // <param name="right"> The object on the right side of the operator. </param> 
        // <returns> True if they are equal, otherwise false. </returns> 
        public static bool operator ==(AzureLocation left, AzureLocation right); 
        // <summary> 
        // Creates a new location implicitly from a string. 
        // </summary> 
        // <param name="location"> String to be assigned in the Name form. </param> 
        public static implicit operator AzureLocation(string location); 
        // <summary> 
        // Creates a string implicitly from a AzureLocation object. 
        // </summary> 
        // <param name="location"> AzureLocation object to be assigned. </param> 
        public static implicit operator string(AzureLocation location); 
        // <summary> 
        // Compares this <see cref="T:Azure.Core.AzureLocation" /> instance with another object and determines if they are equals. 
        // </summary> 
        // <param name="left"> The object on the left side of the operator. </param> 
        // <param name="right"> The object on the right side of the operator. </param> 
        // <returns> True if they are not equal, otherwise false. </returns> 
        public static bool operator !=(AzureLocation left, AzureLocation right); 
        // <summary> 
        // Detects if a location object is equal to another location instance or a string representing the location name. 
        // </summary> 
        // <param name="other"> AzureLocation object or name as a string. </param> 
        // <returns> True or false. </returns> 
        public bool Equals(AzureLocation other); 
        // <summary>Indicates whether this instance and a specified object are equal.</summary><param name="obj">The object to compare with the current instance. </param><returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns> 
        public override bool Equals(object? obj); 
        // <summary>Returns the hash code for this instance.</summary><returns>A 32-bit signed integer that is the hash code for this instance.</returns> 
        public override int GetHashCode(); 
        // <summary> 
        // Gets the name of a location object. 
        // </summary> 
        // <returns> The name. </returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Base type for all client option types, exposes various common client options like <see cref="P:Azure.Core.ClientOptions.Diagnostics" />, <see cref="P:Azure.Core.ClientOptions.Retry" />, <see cref="P:Azure.Core.ClientOptions.Transport" />. 
    // </summary> 
    public abstract class ClientOptions { 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.ClientOptions" />. 
        // </summary> 
        protected ClientOptions(); 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.ClientOptions" /> with the specified <see cref="T:Azure.Core.DiagnosticsOptions" />. 
        // </summary> 
        // <param name="diagnostics"><see cref="T:Azure.Core.DiagnosticsOptions" /> to be used for <see cref="P:Azure.Core.ClientOptions.Diagnostics" />.</param> 
        protected ClientOptions(DiagnosticsOptions? diagnostics); 
        // <summary> 
        // Gets the default set of <see cref="T:Azure.Core.ClientOptions" />. Changes to the <see cref="P:Azure.Core.ClientOptions.Default" /> options would be reflected 
        // in new instances of <see cref="T:Azure.Core.ClientOptions" /> type created after changes to <see cref="P:Azure.Core.ClientOptions.Default" /> were made. 
        // </summary> 
        public static ClientOptions Default { get; }
        // <summary> 
        // Gets the client diagnostic options. 
        // </summary> 
        public DiagnosticsOptions Diagnostics { get; }
        // <summary> 
        // Gets the client retry options. 
        // </summary> 
        public RetryOptions Retry { get; }
        // <summary> 
        // Gets or sets the policy to use for retries. If a policy is specified, it will be used in place of the <see cref="P:Azure.Core.ClientOptions.Retry" /> property. 
        // The <see cref="T:Azure.Core.Pipeline.RetryPolicy" /> type can be derived from to modify the default behavior without needing to fully implement the retry logic. 
        // If <see cref="M:Azure.Core.Pipeline.RetryPolicy.Process(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> is overridden or a custom <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> is specified, 
        // it is the implementer's responsibility to update the <see cref="P:Azure.Core.HttpMessage.ProcessingContext" /> values. 
        // </summary> 
        public HttpPipelinePolicy? RetryPolicy { get; set; }
        // <summary> 
        // The <see cref="T:Azure.Core.Pipeline.HttpPipelineTransport" /> to be used for this client. Defaults to an instance of <see cref="T:Azure.Core.Pipeline.HttpClientTransport" />. 
        // </summary> 
        public HttpPipelineTransport Transport { get; set; }
        // <summary> 
        // Adds an <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> policy into the client pipeline. The position of policy in the pipeline is controlled by the <paramref name="position" /> parameter. 
        // If you want the policy to execute once per client request use <see cref="F:Azure.Core.HttpPipelinePosition.PerCall" /> otherwise use <see cref="F:Azure.Core.HttpPipelinePosition.PerRetry" /> 
        // to run the policy for every retry. Note that the same instance of <paramref name="policy" /> would be added to all pipelines of client constructed using this <see cref="T:Azure.Core.ClientOptions" /> object. 
        // </summary> 
        // <param name="policy">The <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> instance to be added to the pipeline.</param> 
        // <param name="position">The position of policy in the pipeline.</param> 
        public void AddPolicy(HttpPipelinePolicy policy, HttpPipelinePosition position); 
        // <summary>Determines whether the specified object is equal to the current object.</summary><param name="obj">The object to compare with the current object. </param><returns>true if the specified object  is equal to the current object; otherwise, false.</returns> 
        public override bool Equals(object? obj); 
        // <summary>Serves as the default hash function. </summary><returns>A hash code for the current object.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns a string that represents the current object.</summary><returns>A string that represents the current object.</returns> 
        public override string? ToString(); 
    } 

    // <summary> 
    // Represents content type. 
    // </summary> 
    public readonly struct ContentType : IEquatable<ContentType>, IEquatable<string> { 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.ContentType" />. 
        // </summary> 
        // <param name="contentType">The content type string.</param> 
        public ContentType(string contentType); 
        // <summary> 
        // application/json 
        // </summary> 
        public static ContentType ApplicationJson { get; }
        // <summary> 
        // application/octet-stream 
        // </summary> 
        public static ContentType ApplicationOctetStream { get; }
        // <summary> 
        // text/plain 
        // </summary> 
        public static ContentType TextPlain { get; }
        // <summary> 
        // Compares equality of two <see cref="T:Azure.Core.ContentType" /> instances. 
        // </summary> 
        // <param name="left">The method to compare.</param> 
        // <param name="right">The method to compare against.</param> 
        // <returns><c>true</c> if <see cref="T:Azure.Core.ContentType" /> values are equal for <paramref name="left" /> and <paramref name="right" />, otherwise <c>false</c>.</returns> 
        public static bool operator ==(ContentType left, ContentType right); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.ContentType" />. 
        // </summary> 
        // <param name="contentType">The content type string.</param> 
        public static implicit operator ContentType(string contentType); 
        // <summary> 
        // Compares inequality of two <see cref="T:Azure.Core.ContentType" /> instances. 
        // </summary> 
        // <param name="left">The method to compare.</param> 
        // <param name="right">The method to compare against.</param> 
        // <returns><c>true</c> if <see cref="T:Azure.Core.ContentType" /> values are equal for <paramref name="left" /> and <paramref name="right" />, otherwise <c>false</c>.</returns> 
        public static bool operator !=(ContentType left, ContentType right); 
        // <summary>Indicates whether the current object is equal to another object of the same type.</summary><param name="other">An object to compare with this object.</param><returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns> 
        public bool Equals(ContentType other); 
        // <summary>Indicates whether the current object is equal to another object of the same type.</summary><param name="other">An object to compare with this object.</param><returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns> 
        public bool Equals(string? other); 
        // <summary>Indicates whether this instance and a specified object are equal.</summary><param name="obj">The object to compare with the current instance. </param><returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns> 
        public override bool Equals(object? obj); 
        // <summary>Returns the hash code for this instance.</summary><returns>A 32-bit signed integer that is the hash code for this instance.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns the fully qualified type name of this instance.</summary><returns>The fully qualified type name.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // An abstraction to control delay behavior. 
    // </summary> 
    public abstract class DelayStrategy { 
        // <summary> 
        // Constructs a new instance of <see cref="T:Azure.Core.DelayStrategy" />. This constructor can be used by derived classes to customize the jitter factor and max delay. 
        // </summary> 
        // <param name="maxDelay">The max delay value to apply on an individual delay.</param> 
        // <param name="jitterFactor">The jitter factor to apply to each delay. For example, if the delay is 1 second with a jitterFactor of 0.2, the actual 
        // delay used will be a random double between 0.8 and 1.2. If set to 0, no jitter will be applied.</param> 
        protected DelayStrategy(TimeSpan? maxDelay = null, double jitterFactor = 0.2); 
        // <summary> 
        // Constructs an exponential delay with jitter. 
        // </summary> 
        // <param name="initialDelay">The initial delay to use.</param> 
        // <param name="maxDelay">The maximum delay to use.</param> 
        // <returns>The <see cref="T:Azure.Core.DelayStrategy" /> instance.</returns> 
        public static DelayStrategy CreateExponentialDelayStrategy(TimeSpan? initialDelay = null, TimeSpan? maxDelay = null); 
        // <summary> 
        // Constructs a fixed delay with jitter. 
        // </summary> 
        // <param name="delay">The delay to use.</param> 
        // <returns>The <see cref="T:Azure.Core.DelayStrategy" /> instance.</returns> 
        public static DelayStrategy CreateFixedDelayStrategy(TimeSpan? delay = null); 
        // <summary> 
        // Gets the maximum of two <see cref="T:System.TimeSpan" /> values. 
        // </summary> 
        // <param name="val1">The first value.</param> 
        // <param name="val2">The second value.</param> 
        // <returns>The maximum of the two <see cref="T:System.TimeSpan" /> values.</returns> 
        protected static TimeSpan Max(TimeSpan val1, TimeSpan val2); 
        // <summary> 
        // Gets the minimum of two <see cref="T:System.TimeSpan" /> values. 
        // </summary> 
        // <param name="val1">The first value.</param> 
        // <param name="val2">The second value.</param> 
        // <returns>The minimum of the two <see cref="T:System.TimeSpan" /> values.</returns> 
        protected static TimeSpan Min(TimeSpan val1, TimeSpan val2); 
        // <summary> 
        // Gets the next delay interval taking into account the Max Delay, jitter, and any Retry-After headers. 
        // </summary> 
        // <param name="response">The response, if any, returned from the service.</param> 
        // <param name="retryNumber">The retry number.</param> 
        // <returns>A <see cref="T:System.TimeSpan" /> representing the next delay interval.</returns> 
        public TimeSpan GetNextDelay(Response? response, int retryNumber); 
        // <summary> 
        // Gets the next delay interval. Implement this method to provide custom delay logic. 
        // The Max Delay, jitter, and any Retry-After headers will be applied to the value returned from this method. 
        // </summary> 
        // <param name="response">The response, if any, returned from the service.</param> 
        // <param name="retryNumber">The retry number.</param> 
        // <returns>A <see cref="T:System.TimeSpan" /> representing the next delay interval.</returns> 
        protected abstract TimeSpan GetNextDelayCore(Response? response, int retryNumber); 
    } 

    // <summary> 
    // A factory for creating a delegated <see cref="T:Azure.Core.TokenCredential" /> capable of providing an OAuth token. 
    // </summary> 
    public static class DelegatedTokenCredential { 
        // <summary> 
        // Creates a static <see cref="T:Azure.Core.TokenCredential" /> that accepts delegates which will produce an <see cref="T:Azure.Core.AccessToken" />. 
        // </summary> 
        // <remarks> 
        // Typically, the <see cref="T:Azure.Core.TokenCredential" /> created by this method is for use when you have already obtained an <see cref="T:Azure.Core.AccessToken" /> 
        // from some other source and need a <see cref="T:Azure.Core.TokenCredential" /> that will simply return that token. Because the static token can expire, 
        // the delegates offer a mechanism to handle <see cref="T:Azure.Core.AccessToken" /> renewal. 
        // </remarks> 
        // <param name="getToken">A delegate that returns an <see cref="T:Azure.Core.AccessToken" />.</param> 
        // <param name="getTokenAsync">A delegate that returns a <see cref="T:System.Threading.Tasks.ValueTask" /> of type <see cref="T:Azure.Core.AccessToken" />.</param> 
        // <returns></returns> 
        public static TokenCredential Create(Func<TokenRequestContext, CancellationToken, AccessToken> getToken, Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> getTokenAsync); 
        // <summary> 
        // Creates a static <see cref="T:Azure.Core.TokenCredential" /> that accepts delegates which will produce an <see cref="T:Azure.Core.AccessToken" />. 
        // </summary> 
        // <remarks> 
        // Typically, the <see cref="T:Azure.Core.TokenCredential" /> created by this method is for use when you have already obtained an <see cref="T:Azure.Core.AccessToken" /> 
        // from some other source and need a <see cref="T:Azure.Core.TokenCredential" /> that will simply return that token. Because the static token can expire, 
        // the delegates offer a mechanism to handle <see cref="T:Azure.Core.AccessToken" /> renewal. 
        // </remarks> 
        // <param name="getToken">A delegate that returns an <see cref="T:Azure.Core.AccessToken" />.</param> 
        // <returns></returns> 
        public static TokenCredential Create(Func<TokenRequestContext, CancellationToken, AccessToken> getToken); 
    } 

    // <summary> 
    // Exposes client options related to logging, telemetry, and distributed tracing. 
    // </summary> 
    public class DiagnosticsOptions { 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.DiagnosticsOptions" /> with default values. 
        // </summary> 
        protected DiagnosticsOptions(); 
        // <summary> 
        // Gets or sets the value sent as the first part of "User-Agent" headers for all requests issues by this client. Defaults to <see cref="P:Azure.Core.DiagnosticsOptions.DefaultApplicationId" />. 
        // </summary> 
        public string? ApplicationId { get; set; }
        // <summary> 
        // Gets or sets the default application id. Default application id would be set on all instances. 
        // </summary> 
        public static string? DefaultApplicationId { get; set; }
        // <summary> 
        // Gets or sets value indicating whether distributed tracing activities (<see cref="T:System.Diagnostics.Activity" />) are going to be created for the clients methods calls and HTTP calls. 
        // </summary> 
        public bool IsDistributedTracingEnabled { get; set; }
        // <summary> 
        // Gets or sets value indicating if request or response content should be logged. 
        // </summary> 
        public bool IsLoggingContentEnabled { get; set; }
        // <summary> 
        // Get or sets value indicating whether HTTP pipeline logging is enabled. 
        // </summary> 
        public bool IsLoggingEnabled { get; set; }
        // <summary> 
        // Gets or sets value indicating whether the "User-Agent" header containing <see cref="P:Azure.Core.DiagnosticsOptions.ApplicationId" />, client library package name and version, <see cref="P:System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription" /> 
        // and <see cref="P:System.Runtime.InteropServices.RuntimeInformation.OSDescription" /> should be sent. 
        // The default value can be controlled process wide by setting <c>AZURE_TELEMETRY_DISABLED</c> to <c>true</c>, <c>false</c>, <c>1</c> or <c>0</c>. 
        // </summary> 
        public bool IsTelemetryEnabled { get; set; }
        // <summary> 
        // Gets or sets value indicating maximum size of content to log in bytes. Defaults to 4096. 
        // </summary> 
        public int LoggedContentSizeLimit { get; set; }
        // <summary> 
        // Gets a list of header names that are not redacted during logging. 
        // </summary> 
        public IList<string> LoggedHeaderNames { get; }
        // <summary> 
        // Gets a list of query parameter names that are not redacted during logging. 
        // </summary> 
        public IList<string> LoggedQueryParameters { get; }
    } 

    // <summary> 
    // Represents an HTTP header. 
    // </summary> 
    public readonly struct HttpHeader : IEquatable<HttpHeader> { 
        // <summary> 
        // Commonly defined header values. 
        // </summary> 
        public static class Common { 
            // <summary> 
            // Returns header with name "ContentType" and value "application/x-www-form-urlencoded". 
            // </summary> 
            public static readonly HttpHeader FormUrlEncodedContentType; 
            // <summary> 
            // Returns header with name "Accept" and value "application/json". 
            // </summary> 
            public static readonly HttpHeader JsonAccept; 
            // <summary> 
            // Returns header with name "ContentType" and value "application/json". 
            // </summary> 
            public static readonly HttpHeader JsonContentType; 
            // <summary> 
            // Returns header with name "ContentType" and value "application/octet-stream". 
            // </summary> 
            public static readonly HttpHeader OctetStreamContentType; 
        } 

        // <summary> 
        // Contains names of commonly used headers. 
        // </summary> 
        public static class Names { 
            // <summary> 
            // Returns. <code>"Accept"</code> 
            // </summary> 
            public static string Accept { get; }
            // <summary> 
            // Returns. <code>"Authorization"</code> 
            // </summary> 
            public static string Authorization { get; }
            // <summary> 
            // Returns <code>"Content-Disposition"</code>. 
            // </summary> 
            public static string ContentDisposition { get; }
            // <summary> 
            // Returns. <code>"Content-Length"</code> 
            // </summary> 
            public static string ContentLength { get; }
            // <summary> 
            // Returns. <code>"Content-Type"</code> 
            // </summary> 
            public static string ContentType { get; }
            // <summary> 
            // Returns. <code>"Date"</code> 
            // </summary> 
            public static string Date { get; }
            // <summary> 
            // Returns. <code>"ETag"</code> 
            // </summary> 
            public static string ETag { get; }
            // <summary> 
            // Returns. <code>"Host"</code> 
            // </summary> 
            public static string Host { get; }
            // <summary> 
            // Returns. <code>"If-Match"</code> 
            // </summary> 
            public static string IfMatch { get; }
            // <summary> 
            // Returns. <code>"If-Modified-Since"</code> 
            // </summary> 
            public static string IfModifiedSince { get; }
            // <summary> 
            // Returns. <code>"If-None-Match"</code> 
            // </summary> 
            public static string IfNoneMatch { get; }
            // <summary> 
            // Returns. <code>"If-Unmodified-Since"</code> 
            // </summary> 
            public static string IfUnmodifiedSince { get; }
            // <summary> 
            // Returns. <code>"Prefer"</code> 
            // </summary> 
            public static string Prefer { get; }
            // <summary> 
            // Returns. <code>"Range"</code> 
            // </summary> 
            public static string Range { get; }
            // <summary> 
            // Returns. <code>"Referer"</code> 
            // </summary> 
            public static string Referer { get; }
            // <summary> 
            // Returns. <code>"User-Agent"</code> 
            // </summary> 
            public static string UserAgent { get; }
            // <summary> 
            // Returns <code>"WWW-Authenticate"</code>. 
            // </summary> 
            public static string WwwAuthenticate { get; }
            // <summary> 
            // Returns. <code>"x-ms-date"</code> 
            // </summary> 
            public static string XMsDate { get; }
            // <summary> 
            // Returns. <code>"x-ms-range"</code> 
            // </summary> 
            public static string XMsRange { get; }
            // <summary> 
            // Returns. <code>"x-ms-request-id"</code> 
            // </summary> 
            public static string XMsRequestId { get; }
        } 

        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.HttpHeader" /> with provided name and value. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The header value.</param> 
        public HttpHeader(string name, string value); 
        // <summary> 
        // Gets header name. 
        // </summary> 
        public string Name { get; }
        // <summary> 
        // Gets header value. If the header has multiple values they would be joined with a comma. To get separate values use <see cref="M:Azure.Core.RequestHeaders.TryGetValues(System.String,System.Collections.Generic.IEnumerable{System.String}@)" /> or <see cref="M:Azure.Core.ResponseHeaders.TryGetValues(System.String,System.Collections.Generic.IEnumerable{System.String}@)" />. 
        // </summary> 
        public string Value { get; }
        // <summary>Indicates whether the current object is equal to another object of the same type.</summary><param name="other">An object to compare with this object.</param><returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns> 
        public bool Equals(HttpHeader other); 
        // <summary>Indicates whether this instance and a specified object are equal.</summary><param name="obj">The object to compare with the current instance. </param><returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns> 
        public override bool Equals(object? obj); 
        // <summary>Returns the hash code for this instance.</summary><returns>A 32-bit signed integer that is the hash code for this instance.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns the fully qualified type name of this instance.</summary><returns>The fully qualified type name.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Represents a context flowing through the <see cref="T:Azure.Core.Pipeline.HttpPipeline" />. 
    // </summary> 
    public sealed class HttpMessage : IDisposable { 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.HttpMessage" />. 
        // </summary> 
        // <param name="request">The request.</param> 
        // <param name="responseClassifier">The response classifier.</param> 
        public HttpMessage(Request request, ResponseClassifier responseClassifier); 
        // <summary> 
        // Gets or sets the value indicating if response would be buffered as part of the pipeline. Defaults to true. 
        // </summary> 
        public bool BufferResponse { get; set; }
        // <summary> 
        // The <see cref="T:System.Threading.CancellationToken" /> to be used during the <see cref="T:Azure.Core.HttpMessage" /> processing. 
        // </summary> 
        public CancellationToken CancellationToken { get; }
        // <summary> 
        // Gets the value indicating if the response is set on this message. 
        // </summary> 
        public bool HasResponse { get; }
        // <summary> 
        // Gets or sets the network timeout value for this message. If <c>null</c> the value provided in <see cref="P:Azure.Core.RetryOptions.NetworkTimeout" /> would be used instead. 
        // Defaults to <c>null</c>. 
        // </summary> 
        public TimeSpan? NetworkTimeout { get; set; }
        // <summary> 
        // The processing context for the message. 
        // </summary> 
        public MessageProcessingContext ProcessingContext { get; }
        // <summary> 
        // Gets the <see cref="P:Azure.Core.HttpMessage.Request" /> associated with this message. 
        // </summary> 
        public Request Request { get; }
        // <summary> 
        // Gets the <see cref="P:Azure.Core.HttpMessage.Response" /> associated with this message. Throws an exception if it wasn't set yet. 
        // To avoid the exception use <see cref="P:Azure.Core.HttpMessage.HasResponse" /> property to check. 
        // </summary> 
        public Response Response { get; set; }
        // <summary> 
        // The <see cref="P:Azure.Core.HttpMessage.ResponseClassifier" /> instance to use for response classification during pipeline invocation. 
        // </summary> 
        public ResponseClassifier ResponseClassifier { get; set; }
        // <summary> 
        // Disposes the request and response. 
        // </summary> 
        public void Dispose(); 
        // <summary> 
        // Returns the response content stream and releases it ownership to the caller. After calling this methods using <see cref="P:Azure.Response.ContentStream" /> or <see cref="P:Azure.Response.Content" /> would result in exception. 
        // </summary> 
        // <returns>The content stream or null if response didn't have any.</returns> 
        public Stream? ExtractResponseContent(); 
        // <summary> 
        // Sets a property that modifies the pipeline behavior. Please refer to individual policies documentation on what properties it supports. 
        // </summary> 
        // <param name="name">The property name.</param> 
        // <param name="value">The property value.</param> 
        public void SetProperty(string name, object value); 
        // <summary> 
        // Sets a property that is stored with this <see cref="T:Azure.Core.HttpMessage" /> instance and can be used for modifying pipeline behavior. 
        // Internal properties can be keyed with internal types to prevent external code from overwriting these values. 
        // </summary> 
        // <param name="type">The key for the value.</param> 
        // <param name="value">The property value.</param> 
        public void SetProperty(Type type, object value); 
        // <summary> 
        // Gets a property that modifies the pipeline behavior. Please refer to individual policies documentation on what properties it supports. 
        // </summary> 
        // <param name="name">The property name.</param> 
        // <param name="value">The property value.</param> 
        // <returns><c>true</c> if property exists, otherwise. <c>false</c>.</returns> 
        public bool TryGetProperty(string name, out object? value); 
        // <summary> 
        // Gets a property that is stored with this <see cref="T:Azure.Core.HttpMessage" /> instance and can be used for modifying pipeline behavior. 
        // </summary> 
        // <param name="type">The property type.</param> 
        // <param name="value">The property value.</param> 
        // <remarks> 
        // The key value is of type <c>Type</c> for a couple of reasons. Primarily, it allows values to be stored such that though the accessor methods 
        // are public, storing values keyed by internal types make them inaccessible to other assemblies. This protects internal values from being overwritten 
        // by external code. See the <see cref="T:Azure.Core.TelemetryDetails" /> and <see cref="T:Azure.Core.Pipeline.UserAgentValueKey" /> types for an example of this usage. Secondly, <c>Type</c> 
        // comparisons are faster than string comparisons. 
        // </remarks> 
        // <returns><c>true</c> if property exists, otherwise. <c>false</c>.</returns> 
        public bool TryGetProperty(Type type, out object? value); 
    } 

    // <summary> 
    // Represents a position of the policy in the pipeline. 
    // </summary> 
    public enum HttpPipelinePosition { 
        // <summary> 
        // The policy would be invoked once per pipeline invocation (service call). 
        // </summary> 
        PerCall = 0, 
        // <summary> 
        // The policy would be invoked every time request is retried. 
        // </summary> 
        PerRetry = 1, 
        // <summary> 
        // The policy would be invoked before the request is sent by the transport. 
        // </summary> 
        BeforeTransport = 2, 
    } 

    // <summary> 
    // Contains information related to the processing of the <see cref="T:Azure.Core.HttpMessage" /> as it traverses the pipeline. 
    // </summary> 
    public readonly struct MessageProcessingContext { 
        // <summary> 
        // The retry number for the request. For the initial request, the value is 0. 
        // </summary> 
        public int RetryNumber { get; set; }
        // <summary> 
        // The time that the pipeline processing started for the message. 
        // </summary> 
        public DateTimeOffset StartTime { get; }
    } 

    // <summary> 
    // Provides support for creating and parsing multipart/mixed content. 
    // This is implementing a couple of layered standards as mentioned at 
    // https://docs.microsoft.com/en-us/rest/api/storageservices/blob-batch and 
    // https://docs.microsoft.com/en-us/rest/api/storageservices/performing-entity-group-transactions 
    // including https://www.odata.org/documentation/odata-version-3-0/batch-processing/ 
    // and https://www.ietf.org/rfc/rfc2046.txt. 
    // </summary> 
    public static class MultipartResponse { 
        // <summary> 
        // Parse a multipart/mixed response body into several responses. 
        // </summary> 
        // <param name="response">The response containing multi-part content.</param> 
        // <param name="expectCrLf">Controls whether the parser will expect all multi-part boundaries to use CRLF line breaks. This should be true unless more permissive line break parsing is required.</param> 
        // <param name="cancellationToken"> 
        // Optional <see cref="T:System.Threading.CancellationToken" /> to propagate notifications 
        // that the operation should be cancelled. 
        // </param> 
        // <returns>The parsed <see cref="T:Azure.Response" />s.</returns> 
        public static Response[] Parse(Response response, bool expectCrLf, CancellationToken cancellationToken); 
        // <summary> 
        // Parse a multipart/mixed response body into several responses. 
        // </summary> 
        // <param name="response">The response containing multi-part content.</param> 
        // <param name="expectCrLf">Controls whether the parser will expect all multi-part boundaries to use CRLF line breaks. This should be true unless more permissive line break parsing is required.</param> 
        // <param name="cancellationToken"> 
        // Optional <see cref="T:System.Threading.CancellationToken" /> to propagate notifications 
        // that the operation should be cancelled. 
        // </param> 
        // <returns>The parsed <see cref="T:Azure.Response" />s.</returns> 
        public static Task<Response[]> ParseAsync(Response response, bool expectCrLf, CancellationToken cancellationToken); 
    } 

    // <summary> 
    // Represents a token that can be used to rehydrate a long-running operation. 
    // </summary> 
    public readonly struct RehydrationToken : IJsonModel<RehydrationToken>, IPersistableModel<RehydrationToken>, IJsonModel<object>, IPersistableModel<object> { 
        // <summary> 
        // Gets an ID representing the operation that can be used to poll for 
        // the status of the long-running operation. 
        // There are cases that operation id is not available, we return "NOT_SET" for unavailable operation id. 
        // </summary> 
        public string Id { get; }
        // <summary> 
        // Reads one JSON value (including objects or arrays) from the provided reader and converts it to a model. 
        // </summary><param name="reader">The <see cref="T:System.Text.Json.Utf8JsonReader" /> to read.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A <typeparamref name="T" /> representation of the JSON value.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        RehydrationToken IJsonModel<RehydrationToken>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options); 
        // <summary> 
        // Writes the model to the provided <see cref="T:System.Text.Json.Utf8JsonWriter" />. 
        // </summary><param name="writer">The <see cref="T:System.Text.Json.Utf8JsonWriter" /> to write into.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        void IJsonModel<RehydrationToken>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options); 
        // <summary> 
        // Reads one JSON value (including objects or arrays) from the provided reader and converts it to a model. 
        // </summary><param name="reader">The <see cref="T:System.Text.Json.Utf8JsonReader" /> to read.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A <typeparamref name="T" /> representation of the JSON value.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        object IJsonModel<object>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options); 
        // <summary> 
        // Writes the model to the provided <see cref="T:System.Text.Json.Utf8JsonWriter" />. 
        // </summary><param name="writer">The <see cref="T:System.Text.Json.Utf8JsonWriter" /> to write into.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        void IJsonModel<object>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options); 
        // <summary> 
        // Converts the provided <see cref="T:System.BinaryData" /> into a model. 
        // </summary><param name="data">The <see cref="T:System.BinaryData" /> to parse.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A <typeparamref name="T" /> representation of the data.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        RehydrationToken IPersistableModel<RehydrationToken>.Create(BinaryData data, ModelReaderWriterOptions options); 
        // <summary> 
        // Gets the data interchange format (JSON, Xml, etc) that the model uses when communicating with the service. 
        // </summary><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to consider when serializing and deserializing the model.</param><returns>The format that the model uses when communicating with the service.</returns> 
        string IPersistableModel<RehydrationToken>.GetFormatFromOptions(ModelReaderWriterOptions options); 
        // <summary> 
        // Writes the model into a <see cref="T:System.BinaryData" />. 
        // </summary><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A binary representation of the written model.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        BinaryData IPersistableModel<RehydrationToken>.Write(ModelReaderWriterOptions options); 
        // <summary> 
        // Converts the provided <see cref="T:System.BinaryData" /> into a model. 
        // </summary><param name="data">The <see cref="T:System.BinaryData" /> to parse.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A <typeparamref name="T" /> representation of the data.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        object IPersistableModel<object>.Create(BinaryData data, ModelReaderWriterOptions options); 
        // <summary> 
        // Gets the data interchange format (JSON, Xml, etc) that the model uses when communicating with the service. 
        // </summary><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to consider when serializing and deserializing the model.</param><returns>The format that the model uses when communicating with the service.</returns> 
        string IPersistableModel<object>.GetFormatFromOptions(ModelReaderWriterOptions options); 
        // <summary> 
        // Writes the model into a <see cref="T:System.BinaryData" />. 
        // </summary><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A binary representation of the written model.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        BinaryData IPersistableModel<object>.Write(ModelReaderWriterOptions options); 
    } 

    // <summary> 
    // Represents an HTTP request. Use <see cref="M:Azure.Core.Pipeline.HttpPipeline.CreateMessage" /> or <see cref="M:Azure.Core.Pipeline.HttpPipeline.CreateRequest" /> to create an instance. 
    // </summary> 
    public abstract class Request : IDisposable { 
        protected Request(); 
        // <summary> 
        // Gets or sets the client request id that was sent to the server as <c>x-ms-client-request-id</c> headers. 
        // </summary> 
        public abstract string ClientRequestId { get; set; }
        // <summary> 
        // Gets or sets the request content. 
        // </summary> 
        public virtual RequestContent? Content { get; set; }
        // <summary> 
        // Gets the response HTTP headers. 
        // </summary> 
        public RequestHeaders Headers { get; }
        // <summary> 
        // Gets or sets the request HTTP method. 
        // </summary> 
        public virtual RequestMethod Method { get; set; }
        // <summary> 
        // Gets or sets and instance of <see cref="T:Azure.Core.RequestUriBuilder" /> used to create the Uri. 
        // </summary> 
        public virtual RequestUriBuilder Uri { get; set; }
        // <summary> 
        // Frees resources held by this <see cref="T:Azure.Response" /> instance. 
        // </summary> 
        public abstract void Dispose(); 
        // <summary> 
        // Adds a header value to the header collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The header value.</param> 
        protected abstract void AddHeader(string name, string value); 
        // <summary> 
        // Returns <c>true</c> if the header is stored in the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        protected abstract bool ContainsHeader(string name); 
        // <summary> 
        // Returns an iterator enumerating <see cref="T:Azure.Core.HttpHeader" /> in the request. 
        // </summary> 
        // <returns>The <see cref="T:System.Collections.Generic.IEnumerable`1" /> enumerating <see cref="T:Azure.Core.HttpHeader" /> in the response.</returns> 
        protected abstract IEnumerable<HttpHeader> EnumerateHeaders(); 
        // <summary> 
        // Removes the header from the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        protected abstract bool RemoveHeader(string name); 
        // <summary> 
        // Sets a header value the header collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The header value.</param> 
        protected virtual void SetHeader(string name, string value); 
        // <summary> 
        // Returns header value if the header is stored in the collection. If the header has multiple values they are going to be joined with a comma. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The reference to populate with value.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        protected abstract bool TryGetHeader(string name, out string? value); 
        // <summary> 
        // Returns header values if the header is stored in the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="values">The reference to populate with values.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        protected abstract bool TryGetHeaderValues(string name, out IEnumerable<string>? values); 
    } 

    // <summary> 
    // Represents the content sent as part of the <see cref="T:Azure.Core.Request" />. 
    // </summary> 
    public abstract class RequestContent : IDisposable { 
        protected RequestContent(); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.IO.Stream" />. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.IO.Stream" />.</returns> 
        public static RequestContent Create(Stream stream); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps an <see cref="T:System.Array" />of <see cref="T:System.Byte" />. 
        // </summary> 
        // <param name="bytes">The <see cref="T:System.Array" />of <see cref="T:System.Byte" /> to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps provided <see cref="T:System.Array" />of <see cref="T:System.Byte" />.</returns> 
        public static RequestContent Create(byte[] bytes); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps an <see cref="T:System.Array" />of <see cref="T:System.Byte" />. 
        // </summary> 
        // <param name="bytes">The <see cref="T:System.Array" />of <see cref="T:System.Byte" /> to use.</param> 
        // <param name="index">The offset in <paramref name="bytes" /> to start from.</param> 
        // <param name="length">The length of the segment to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps provided <see cref="T:System.Array" />of <see cref="T:System.Byte" />.</returns> 
        public static RequestContent Create(byte[] bytes, int index, int length); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.IO.Stream" />. 
        // </summary> 
        // <param name="bytes">The <see cref="T:System.ReadOnlyMemory`1" /> to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.ReadOnlyMemory`1" />.</returns> 
        public static RequestContent Create(ReadOnlyMemory<byte> bytes); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.Buffers.ReadOnlySequence`1" />. 
        // </summary> 
        // <param name="bytes">The <see cref="T:System.Buffers.ReadOnlySequence`1" /> to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.Buffers.ReadOnlySequence`1" />.</returns> 
        public static RequestContent Create(ReadOnlySequence<byte> bytes); 
        // <summary> 
        // Creates a RequestContent representing the UTF-8 Encoding of the given <see cref="T:System.String" />/ 
        // </summary> 
        // <param name="content">The <see cref="T:System.String" /> to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.String" />.</returns> 
        // <remarks>The returned content represents the UTF-8 Encoding of the given string.</remarks> 
        public static RequestContent Create(string content); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.BinaryData" />. 
        // </summary> 
        // <param name="content">The <see cref="T:System.BinaryData" /> to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:System.BinaryData" />.</returns> 
        public static RequestContent Create(BinaryData content); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:Azure.Core.Serialization.DynamicData" />. 
        // </summary> 
        // <param name="content">The <see cref="T:Azure.Core.Serialization.DynamicData" /> to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a <see cref="T:Azure.Core.Serialization.DynamicData" />.</returns> 
        public static RequestContent Create(DynamicData content); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a serialized version of an object. 
        // </summary> 
        // <param name="serializable">The <see cref="T:System.Object" /> to serialize.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a serialized version of the object.</returns> 
        public static RequestContent Create(object serializable); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a serialized version of an object. 
        // </summary> 
        // <param name="serializable">The <see cref="T:System.Object" /> to serialize.</param> 
        // <param name="serializer">The <see cref="T:Azure.Core.Serialization.ObjectSerializer" /> to use to convert the object to bytes. If not provided, <see cref="T:Azure.Core.Serialization.JsonObjectSerializer" /> is used.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a serialized version of the object.</returns> 
        public static RequestContent Create(object serializable, ObjectSerializer? serializer); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a serialized version of an object. 
        // </summary> 
        // <param name="serializable">The <see cref="T:System.Object" /> to serialize.</param> 
        // <param name="propertyNameFormat">The format to use for property names in the serialized content.</param> 
        // <param name="dateTimeFormat">The format to use for DateTime and DateTimeOffset values in the serialized content.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps a serialized version of the object.</returns> 
        public static RequestContent Create(object serializable, JsonPropertyNames propertyNameFormat, string dateTimeFormat = "o"); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestContent" /> that wraps an <see cref="T:System.ClientModel.Primitives.IPersistableModel`1" />. 
        // </summary> 
        // <typeparam name="T">The type of the model.</typeparam> 
        // <param name="model">The <see cref="T:System.ClientModel.Primitives.IPersistableModel`1" /> to use.</param> 
        // <param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param> 
        // <returns>An instance of <see cref="T:Azure.Core.RequestContent" /> that wraps an <see cref="T:System.ClientModel.Primitives.IPersistableModel`1" />.</returns> 
        public static RequestContent Create<T>(T model, ModelReaderWriterOptions? options = null) where T : IPersistableModel<T>; 
        // <summary> 
        // Creates a RequestContent representing the UTF-8 Encoding of the given <see cref="T:System.String" />. 
        // </summary> 
        // <param name="content">The <see cref="T:System.String" /> to use.</param> 
        public static implicit operator RequestContent(string content); 
        // <summary> 
        // Creates a RequestContent that wraps a <see cref="T:System.BinaryData" />. 
        // </summary> 
        // <param name="content">The <see cref="T:System.BinaryData" /> to use.</param> 
        public static implicit operator RequestContent(BinaryData content); 
        // <summary> 
        // Creates a RequestContent that wraps a <see cref="T:Azure.Core.Serialization.DynamicData" />. 
        // </summary> 
        // <param name="content">The <see cref="T:Azure.Core.Serialization.DynamicData" /> to use.</param> 
        public static implicit operator RequestContent(DynamicData content); 
        // <summary> 
        // Frees resources held by the <see cref="T:Azure.Core.RequestContent" /> object. 
        // </summary> 
        public abstract void Dispose(); 
        // <summary> 
        // Attempts to compute the length of the underlying content, if available. 
        // </summary> 
        // <param name="length">The length of the underlying data.</param> 
        public abstract bool TryComputeLength(out long length); 
        // <summary> 
        // Writes contents of this object to an instance of <see cref="T:System.IO.Stream" />. 
        // </summary> 
        // <param name="stream">The stream to write to.</param> 
        // <param name="cancellation">To cancellation token to use.</param> 
        public abstract void WriteTo(Stream stream, CancellationToken cancellation); 
        // <summary> 
        // Writes contents of this object to an instance of <see cref="T:System.IO.Stream" />. 
        // </summary> 
        // <param name="stream">The stream to write to.</param> 
        // <param name="cancellation">To cancellation token to use.</param> 
        public abstract Task WriteToAsync(Stream stream, CancellationToken cancellation); 
    } 

    // <summary> 
    // Controls how error response content should be parsed. 
    // </summary> 
    public abstract class RequestFailedDetailsParser { 
        protected RequestFailedDetailsParser(); 
        // <summary> 
        // Parses the error details from the provided <see cref="T:Azure.Response" />. 
        // </summary> 
        // <remarks> 
        // In various scenarios, parsers may be called for successful responses. Implementations should not populate <paramref name="error" /> or <paramref name="data" /> with sensitive information, as these values may be logged as part of the exception. 
        // </remarks> 
        // <param name="response">The <see cref="T:Azure.Response" /> to parse. The <see cref="P:Azure.Response.ContentStream" /> will already be buffered.</param> 
        // <param name="error">The <see cref="T:Azure.ResponseError" /> describing the parsed error details.</param> 
        // <param name="data">Data to be applied to the <see cref="P:System.Exception.Data" /> property.</param> 
        // <returns><c>true</c> if successful, otherwise <c>false</c>.</returns> 
        public abstract bool TryParse(Response response, out ResponseError? error, out IDictionary<string, string>? data); 
    } 

    // <summary> 
    // Headers to be sent as part of the <see cref="T:Azure.Core.Request" />. 
    // </summary> 
    public readonly struct RequestHeaders : IEnumerable<HttpHeader>, IEnumerable { 
        // <summary> 
        // Adds the <see cref="T:Azure.Core.HttpHeader" /> instance to the collection. 
        // </summary> 
        // <param name="header">The header to add.</param> 
        public void Add(HttpHeader header); 
        // <summary> 
        // Adds the header to the collection. If a header with this name already exist adds an additional value to the header values. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The header value.</param> 
        public void Add(string name, string value); 
        // <summary> 
        // Returns <c>true</c> if the headers is stored in the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        public bool Contains(string name); 
        // <summary> 
        // Returns an enumerator that iterates through the <see cref="T:Azure.Core.RequestHeaders" />. 
        // </summary> 
        // <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> for the <see cref="T:Azure.Core.RequestHeaders" />.</returns> 
        public IEnumerator<HttpHeader> GetEnumerator(); 
        // <summary> 
        // Removes the header from the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <returns><c>true</c> if the header existed, otherwise <c>false</c>.</returns> 
        public bool Remove(string name); 
        // <summary> 
        // Sets the header value name. If a header with this name already exist replaces it's value. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The header value.</param> 
        public void SetValue(string name, string value); 
        // <summary> 
        // Returns header value if the headers is stored in the collection. If the header has multiple values they are going to be joined with a comma. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The reference to populate with value.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        public bool TryGetValue(string name, out string? value); 
        // <summary> 
        // Returns header values if the header is stored in the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="values">The reference to populate with values.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        public bool TryGetValues(string name, out IEnumerable<string>? values); 
        // <summary> 
        // Returns an enumerator that iterates through the <see cref="T:Azure.Core.RequestHeaders" />. 
        // </summary> 
        // <returns>A <see cref="T:System.Collections.IEnumerator" /> for the <see cref="T:Azure.Core.RequestHeaders" />.</returns> 
        IEnumerator IEnumerable.GetEnumerator(); 
    } 

    // <summary> 
    // Represents HTTP methods sent as part of a <see cref="T:Azure.Core.Request" />. 
    // </summary> 
    public readonly struct RequestMethod : IEquatable<RequestMethod> { 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.RequestMethod" /> with provided method. Method must be all uppercase. 
        // Prefer <see cref="M:Azure.Core.RequestMethod.Parse(System.String)" /> if <paramref name="method" /> can be one of predefined method names. 
        // </summary> 
        // <param name="method">The method to use.</param> 
        public RequestMethod(string method); 
        // <summary> 
        // Gets <see cref="T:Azure.Core.RequestMethod" /> instance for DELETE method. 
        // </summary> 
        public static RequestMethod Delete { get; }
        // <summary> 
        // Gets <see cref="T:Azure.Core.RequestMethod" /> instance for GET method. 
        // </summary> 
        public static RequestMethod Get { get; }
        // <summary> 
        // Gets <see cref="T:Azure.Core.RequestMethod" /> instance for HEAD method. 
        // </summary> 
        public static RequestMethod Head { get; }
        // <summary> 
        // Gets the HTTP method. 
        // </summary> 
        public string Method { get; }
        // <summary> 
        // Gets <see cref="T:Azure.Core.RequestMethod" /> instance for OPTIONS method. 
        // </summary> 
        public static RequestMethod Options { get; }
        // <summary> 
        // Gets <see cref="T:Azure.Core.RequestMethod" /> instance for PATCH method. 
        // </summary> 
        public static RequestMethod Patch { get; }
        // <summary> 
        // Gets <see cref="T:Azure.Core.RequestMethod" /> instance for POST method. 
        // </summary> 
        public static RequestMethod Post { get; }
        // <summary> 
        // Gets <see cref="T:Azure.Core.RequestMethod" /> instance for PUT method. 
        // </summary> 
        public static RequestMethod Put { get; }
        // <summary> 
        // Gets <see cref="T:Azure.Core.RequestMethod" /> instance for TRACE method. 
        // </summary> 
        public static RequestMethod Trace { get; }
        // <summary> 
        // Compares equality of two <see cref="T:Azure.Core.RequestMethod" /> instances. 
        // </summary> 
        // <param name="left">The method to compare.</param> 
        // <param name="right">The method to compare against.</param> 
        // <returns><c>true</c> if <see cref="P:Azure.Core.RequestMethod.Method" /> values are equal for <paramref name="left" /> and <paramref name="right" />, otherwise <c>false</c>.</returns> 
        public static bool operator ==(RequestMethod left, RequestMethod right); 
        // <summary> 
        // Compares inequality of two <see cref="T:Azure.Core.RequestMethod" /> instances. 
        // </summary> 
        // <param name="left">The method to compare.</param> 
        // <param name="right">The method to compare against.</param> 
        // <returns><c>true</c> if <see cref="P:Azure.Core.RequestMethod.Method" /> values are equal for <paramref name="left" /> and <paramref name="right" />, otherwise <c>false</c>.</returns> 
        public static bool operator !=(RequestMethod left, RequestMethod right); 
        // <summary> 
        // Parses string to it's <see cref="T:Azure.Core.RequestMethod" /> representation. 
        // </summary> 
        // <param name="method">The method string to parse.</param> 
        public static RequestMethod Parse(string method); 
        // <summary>Indicates whether the current object is equal to another object of the same type.</summary><param name="other">An object to compare with this object.</param><returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns> 
        public bool Equals(RequestMethod other); 
        // <summary>Indicates whether this instance and a specified object are equal.</summary><param name="obj">The object to compare with the current instance. </param><returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns> 
        public override bool Equals(object? obj); 
        // <summary>Returns the hash code for this instance.</summary><returns>A 32-bit signed integer that is the hash code for this instance.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns the fully qualified type name of this instance.</summary><returns>The fully qualified type name.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Provides a custom builder for Uniform Resource Identifiers (URIs) and modifies URIs for the <see cref="T:System.Uri" /> class. 
    // </summary> 
    public class RequestUriBuilder { 
        public RequestUriBuilder(); 
        // <summary> 
        // Gets or sets the Domain Name System (DNS) host name or IP address of a server. 
        // </summary> 
        public string? Host { get; set; }
        // <summary> 
        // Gets or sets the path to the resource referenced by the URI. 
        // </summary> 
        public string Path { get; set; }
        // <summary> 
        // Gets the path and query string to the resource referenced by the URI. 
        // </summary> 
        public string PathAndQuery { get; }
        // <summary> 
        // Gets or sets the port number of the URI. 
        // </summary> 
        public int Port { get; set; }
        // <summary> 
        // Gets or sets any query information included in the URI. 
        // </summary> 
        public string Query { get; set; }
        // <summary> 
        // Gets or sets the scheme name of the URI. 
        // </summary> 
        public string? Scheme { get; set; }
        // <summary> Gets whether or not this instance of <see cref="T:Azure.Core.RequestUriBuilder" /> has a path. </summary> 
        protected bool HasPath { get; }
        // <summary> Gets whether or not this instance of <see cref="T:Azure.Core.RequestUriBuilder" /> has a query. </summary> 
        protected bool HasQuery { get; }
        // <summary> 
        // Escapes and appends the <paramref name="value" /> to <see cref="P:Azure.Core.RequestUriBuilder.Path" /> without adding path separator. 
        // Path segments and any other characters will be escaped, e.g. ":" will be escaped as "%3a". 
        // </summary> 
        // <param name="value">The value to escape and append.</param> 
        public void AppendPath(string value); 
        // <summary> 
        // Optionally escapes and appends the <paramref name="value" /> to <see cref="P:Azure.Core.RequestUriBuilder.Path" /> without adding path separator. 
        // If <paramref name="escape" /> is true, path segments and any other characters will be escaped, e.g. ":" will be escaped as "%3a". 
        // </summary> 
        // <param name="value">The value to optionally escape and append.</param> 
        // <param name="escape">Whether value should be escaped.</param> 
        public void AppendPath(string value, bool escape); 
        // <summary> 
        // Optionally escapes and appends the <paramref name="value" /> to <see cref="P:Azure.Core.RequestUriBuilder.Path" /> without adding path separator. 
        // If <paramref name="escape" /> is true, path segments and any other characters will be escaped, e.g. ":" will be escaped as "%3a". 
        // </summary> 
        // <param name="value">The value to optionally escape and append.</param> 
        // <param name="escape">Whether value should be escaped.</param> 
        public void AppendPath(ReadOnlySpan<char> value, bool escape); 
        // <summary> 
        // Appends a query parameter adding separator if required. Escapes the value. 
        // </summary> 
        // <param name="name">The name of parameter.</param> 
        // <param name="value">The value of parameter.</param> 
        public void AppendQuery(string name, string value); 
        // <summary> 
        // Appends a query parameter adding separator if required. 
        // </summary> 
        // <param name="name">The name of parameter.</param> 
        // <param name="value">The value of parameter.</param> 
        // <param name="escapeValue">Whether value should be escaped.</param> 
        public void AppendQuery(string name, string value, bool escapeValue); 
        // <summary> 
        // Appends a query parameter adding separator if required. 
        // </summary> 
        // <param name="name">The name of parameter.</param> 
        // <param name="value">The value of parameter.</param> 
        // <param name="escapeValue">Whether value should be escaped.</param> 
        public void AppendQuery(ReadOnlySpan<char> name, ReadOnlySpan<char> value, bool escapeValue); 
        // <summary> 
        // Replaces values inside this instance with values provided in the <paramref name="value" /> parameter. 
        // </summary> 
        // <param name="value">The <see cref="T:System.Uri" /> instance to get values from.</param> 
        public void Reset(Uri value); 
        // <summary> 
        // Gets the <see cref="T:System.Uri" /> instance constructed by the specified <see cref="T:Azure.Core.RequestUriBuilder" /> instance. 
        // </summary> 
        // <returns> 
        // A <see cref="T:System.Uri" /> that contains the URI constructed by the <see cref="T:Azure.Core.RequestUriBuilder" />. 
        // </returns> 
        public Uri ToUri(); 
        // <summary> 
        // Returns a string representation of this <see cref="T:Azure.Core.RequestUriBuilder" />. 
        // </summary> 
        // <returns>A string representation of this <see cref="T:Azure.Core.RequestUriBuilder" />.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // An Azure Resource Manager resource identifier. 
    // </summary> 
    public sealed class ResourceIdentifier : IEquatable<ResourceIdentifier>, IComparable<ResourceIdentifier> { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.Core.ResourceIdentifier" /> class. 
        // </summary> 
        // <param name="resourceId"> The id string to create the ResourceIdentifier from. </param> 
        // <remarks> 
        // For more information on ResourceIdentifier format see the following. 
        // ResourceGroup level id https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/template-functions-resource#resourceid 
        // Subscription level id https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/template-functions-resource#subscriptionresourceid 
        // Tenant level id https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/template-functions-resource#tenantresourceid 
        // Extension id https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/template-functions-resource#extensionresourceid 
        // </remarks> 
        public ResourceIdentifier(string resourceId); 
        // <summary> 
        // The root of the resource hierarchy. 
        // </summary> 
        public static readonly ResourceIdentifier Root; 
        // <summary> 
        // Gets the location if it exists otherwise null. 
        // </summary> 
        public AzureLocation? Location { get; }
        // <summary> 
        // The name of the resource. 
        // </summary> 
        public string Name { get; }
        // <summary> 
        // The immediate parent containing this resource. 
        // </summary> 
        public ResourceIdentifier? Parent { get; }
        // <summary> 
        // Gets the provider namespace if it exists otherwise null. 
        // </summary> 
        public string? Provider { get; }
        // <summary> 
        // The name of the resource group if it exists otherwise null. 
        // </summary> 
        public string? ResourceGroupName { get; }
        // <summary> 
        // The resource type of the resource. 
        // </summary> 
        public ResourceType ResourceType { get; }
        // <summary> 
        // Gets the subscription id if it exists otherwise null. 
        // </summary> 
        public string? SubscriptionId { get; }
        // <summary> 
        // Operator overloading for '=='. 
        // </summary> 
        // <param name="left"> Left ResourceIdentifier object to compare. </param> 
        // <param name="right"> Right ResourceIdentifier object to compare. </param> 
        // <returns></returns> 
        public static bool operator ==(ResourceIdentifier left, ResourceIdentifier right); 
        // <summary> 
        // Compares one <see cref="T:Azure.Core.ResourceIdentifier" /> with another instance. 
        // </summary> 
        // <param name="left"> The object on the left side of the operator. </param> 
        // <param name="right"> The object on the right side of the operator. </param> 
        // <returns> True if the left object is greater than the right. </returns> 
        public static bool operator >(ResourceIdentifier left, ResourceIdentifier right); 
        // <summary> 
        // Compares one <see cref="T:Azure.Core.ResourceIdentifier" /> with another instance. 
        // </summary> 
        // <param name="left"> The object on the left side of the operator. </param> 
        // <param name="right"> The object on the right side of the operator. </param> 
        // <returns> True if the left object is greater than or equal to the right. </returns> 
        public static bool operator >=(ResourceIdentifier left, ResourceIdentifier right); 
        // <summary> 
        // Convert a resource identifier to a string. 
        // </summary> 
        // <param name="id"> The resource identifier. </param> 
        public static implicit operator string?(ResourceIdentifier id); 
        // <summary> 
        // Operator overloading for '!='. 
        // </summary> 
        // <param name="left"> Left ResourceIdentifier object to compare. </param> 
        // <param name="right"> Right ResourceIdentifier object to compare. </param> 
        // <returns></returns> 
        public static bool operator !=(ResourceIdentifier left, ResourceIdentifier right); 
        // <summary> 
        // Compares one <see cref="T:Azure.Core.ResourceIdentifier" /> with another instance. 
        // </summary> 
        // <param name="left"> The object on the left side of the operator. </param> 
        // <param name="right"> The object on the right side of the operator. </param> 
        // <returns> True if the left object is less than the right. </returns> 
        public static bool operator <(ResourceIdentifier left, ResourceIdentifier right); 
        // <summary> 
        // Compares one <see cref="T:Azure.Core.ResourceIdentifier" /> with another instance. 
        // </summary> 
        // <param name="left"> The object on the left side of the operator. </param> 
        // <param name="right"> The object on the right side of the operator. </param> 
        // <returns> True if the left object is less than or equal to the right. </returns> 
        public static bool operator <=(ResourceIdentifier left, ResourceIdentifier right); 
        // <summary> 
        // Converts the string representation of a ResourceIdentifier to the equivalent <see cref="T:Azure.Core.ResourceIdentifier" /> structure. 
        // </summary> 
        // <param name="input"> The id string to convert. </param> 
        // <returns> A class that contains the value that was parsed. </returns> 
        // <exception cref="T:System.FormatException"> when resourceId is not a valid <see cref="T:Azure.Core.ResourceIdentifier" /> format. </exception> 
        // <exception cref="T:System.ArgumentNullException"> when resourceId is null. </exception> 
        // <exception cref="T:System.ArgumentException"> when resourceId is empty. </exception> 
        public static ResourceIdentifier Parse(string input); 
        // <summary> 
        // Converts the string representation of a ResourceIdentifier to the equivalent <see cref="T:Azure.Core.ResourceIdentifier" /> structure. 
        // </summary> 
        // <param name="input"> The id string to convert. </param> 
        // <param name="result"> 
        // The structure that will contain the parsed value. 
        // If the method returns true result contains a valid ResourceIdentifier. 
        // If the method returns false, result will be null. 
        // </param> 
        // <returns> True if the parse operation was successful; otherwise, false. </returns> 
        public static bool TryParse(string? input, out ResourceIdentifier? result); 
        // <summary> 
        // Add a provider resource to an existing resource id. 
        // </summary> 
        // <param name="childResourceType"> The simple type of the child resource, without slashes (/), 
        // for example, 'subnets'. </param> 
        // <param name="childResourceName"> The name of the resource. </param> 
        // <returns> The combined resource id. </returns> 
        public ResourceIdentifier AppendChildResource(string childResourceType, string childResourceName); 
        // <summary> 
        // Add a provider resource to an existing resource id. 
        // </summary> 
        // <param name="providerNamespace"> The provider namespace of the added resource. </param> 
        // <param name="resourceType"> The simple type of the added resource, without slashes (/), 
        // for example, 'virtualMachines'. </param> 
        // <param name="resourceName"> The name of the resource.</param> 
        // <returns> The combined resource id. </returns> 
        public ResourceIdentifier AppendProviderResource(string providerNamespace, string resourceType, string resourceName); 
        // <summary> 
        // Compre this resource identifier to the given resource identifier. 
        // </summary> 
        // <param name="other"> The resource identifier to compare to. </param> 
        // <returns> 0 if the resource identifiers are equivalent, less than 0 if this resource identifier 
        // should be ordered before the given resource identifier, greater than 0 if this resource identifier 
        // should be ordered after the given resource identifier. </returns> 
        public int CompareTo(ResourceIdentifier? other); 
        // <summary> 
        // Determine if this resource identifier is equivalent to the given resource identifier. 
        // </summary> 
        // <param name="other"> The resource identifier to compare to. </param> 
        // <returns>True if the resource identifiers are equivalent, otherwise false. </returns> 
        public bool Equals(ResourceIdentifier? other); 
        // <summary>Determines whether the specified object is equal to the current object.</summary><param name="obj">The object to compare with the current object. </param><returns>true if the specified object  is equal to the current object; otherwise, false.</returns> 
        public override bool Equals(object? obj); 
        // <summary>Serves as the default hash function. </summary><returns>A hash code for the current object.</returns> 
        public override int GetHashCode(); 
        // <summary> 
        // Return the string representation of the resource identifier. 
        // </summary> 
        // <returns> The string representation of this resource identifier. </returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Structure representing a resource type. 
    // </summary> 
    // <remarks> See https://docs.microsoft.com/en-us/azure/azure-resource-manager/management/resource-providers-and-types for more info. </remarks> 
    public readonly struct ResourceType : IEquatable<ResourceType>, IComparable<ResourceType> { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.Core.ResourceType" /> class. 
        // </summary> 
        // <param name="resourceType"> The resource type string to convert. </param> 
        public ResourceType(string resourceType); 
        // <summary> 
        // Gets the resource type Namespace. 
        // </summary> 
        public string Namespace { get; }
        // <summary> 
        // Gets the resource Type. 
        // </summary> 
        public string Type { get; }
        // <summary> 
        // Compares two <see cref="T:Azure.Core.ResourceType" /> objects. 
        // </summary> 
        // <param name="left"> First <see cref="T:Azure.Core.ResourceType" /> object. </param> 
        // <param name="right"> Second <see cref="T:Azure.Core.ResourceType" /> object. </param> 
        // <returns> True if they are equal, otherwise False. </returns> 
        public static bool operator ==(ResourceType left, ResourceType right); 
        // <summary> 
        // Implicit operator for initializing a <see cref="T:Azure.Core.ResourceType" /> instance from a string. 
        // </summary> 
        // <param name="resourceType"> String to be converted into a <see cref="T:Azure.Core.ResourceType" /> object. </param> 
        public static implicit operator ResourceType(string resourceType); 
        // <summary> 
        // Implicit operator for initializing a string from a <see cref="T:Azure.Core.ResourceType" />. 
        // </summary> 
        // <param name="resourceType"> <see cref="T:Azure.Core.ResourceType" /> to be converted into a string. </param> 
        public static implicit operator string(ResourceType resourceType); 
        // <summary> 
        // Compares two <see cref="T:Azure.Core.ResourceType" /> objects. 
        // </summary> 
        // <param name="left"> First <see cref="T:Azure.Core.ResourceType" /> object. </param> 
        // <param name="right"> Second <see cref="T:Azure.Core.ResourceType" /> object. </param> 
        // <returns> False if they are equal, otherwise True. </returns> 
        public static bool operator !=(ResourceType left, ResourceType right); 
        // <summary>Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object. </summary><param name="other">An object to compare with this instance. </param><returns>A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance precedes <paramref name="other" /> in the sort order.  Zero This instance occurs in the same position in the sort order as <paramref name="other" />. Greater than zero This instance follows <paramref name="other" /> in the sort order. </returns> 
        public int CompareTo(ResourceType other); 
        // <summary> 
        // Compares this <see cref="T:Azure.Core.ResourceType" /> instance with another object and determines if they are equals. 
        // </summary> 
        // <param name="other"> <see cref="T:Azure.Core.ResourceType" /> object to compare. </param> 
        // <returns> True if they are equals, otherwise false. </returns> 
        public bool Equals(ResourceType other); 
        // <summary> 
        // Gets the last resource type name. 
        // </summary> 
        public string GetLastType(); 
        // <summary>Indicates whether this instance and a specified object are equal.</summary><param name="other">The object to compare with the current instance. </param><returns>true if <paramref name="other" /> and this instance are the same type and represent the same value; otherwise, false. </returns> 
        public override bool Equals(object? other); 
        // <summary>Returns the hash code for this instance.</summary><returns>A 32-bit signed integer that is the hash code for this instance.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns the fully qualified type name of this instance.</summary><returns>The fully qualified type name.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // A type that analyzes an HTTP message and determines if the response it holds 
    // should be treated as an error response. A classifier of this type may use information 
    // from the request, the response, or other message property to decide 
    // whether and how to classify the message. 
    // <para /> 
    // This type's <code>TryClassify</code> method allows chaining together handlers before 
    // applying default classifier logic. 
    // If a handler in the chain returns false from <code>TryClassify</code>, 
    // the next handler will be tried, and so on.  The first handler that returns true 
    // will determine whether the response is an error. 
    // </summary> 
    public abstract class ResponseClassificationHandler { 
        protected ResponseClassificationHandler(); 
        // <summary> 
        // Populates the <code>isError</code> out parameter to indicate whether or not 
        // to classify the message's response as an error. 
        // </summary> 
        // <param name="message">The message to classify.</param> 
        // <param name="isError">Whether the message's response should be considered an error.</param> 
        // <returns><code>true</code> if the handler had a classification for this message; <code>false</code> otherwise.</returns> 
        public abstract bool TryClassify(HttpMessage message, out bool isError); 
    } 

    // <summary> 
    // A type that analyzes HTTP responses and exceptions and determines if they should be retried, 
    // and/or analyzes responses and determines if they should be treated as error responses. 
    // </summary> 
    public class ResponseClassifier { 
        public ResponseClassifier(); 
        // <summary> 
        // Specifies if the response contained in the <paramref name="message" /> is not successful. 
        // </summary> 
        public virtual bool IsErrorResponse(HttpMessage message); 
        // <summary> 
        // Specifies if the operation that caused the exception should be retried taking the <see cref="T:Azure.Core.HttpMessage" /> into consideration. 
        // </summary> 
        public virtual bool IsRetriable(HttpMessage message, Exception exception); 
        // <summary> 
        // Specifies if the operation that caused the exception should be retried. 
        // </summary> 
        public virtual bool IsRetriableException(Exception exception); 
        // <summary> 
        // Specifies if the request contained in the <paramref name="message" /> should be retried. 
        // </summary> 
        public virtual bool IsRetriableResponse(HttpMessage message); 
    } 

    // <summary> 
    // Headers received as part of the <see cref="T:Azure.Response" />. 
    // </summary> 
    public readonly struct ResponseHeaders : IEnumerable<HttpHeader>, IEnumerable { 
        // <summary> 
        // Gets the parsed value of "Content-Length" header. 
        // </summary> 
        public int? ContentLength { get; }
        // <summary> 
        // Gets the parsed value of "Content-Length" header as a long. 
        // </summary> 
        public long? ContentLengthLong { get; }
        // <summary> 
        // Gets the value of "Content-Type" header. 
        // </summary> 
        public string? ContentType { get; }
        // <summary> 
        // Gets the parsed value of "Date" or "x-ms-date" header. 
        // </summary> 
        public DateTimeOffset? Date { get; }
        // <summary> 
        // Gets the parsed value of "ETag" header. 
        // </summary> 
        public ETag? ETag { get; }
        // <summary> 
        // Gets the value of "x-ms-request-id" header. 
        // </summary> 
        public string? RequestId { get; }
        // <summary> 
        // Returns <c>true</c> if the header is stored in the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        public bool Contains(string name); 
        // <summary> 
        // Returns an enumerator that iterates through the <see cref="T:Azure.Core.ResponseHeaders" />. 
        // </summary> 
        // <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> for the <see cref="T:Azure.Core.ResponseHeaders" />.</returns> 
        public IEnumerator<HttpHeader> GetEnumerator(); 
        // <summary> 
        // Returns header value if the header is stored in the collection. If header has multiple values they are going to be joined with a comma. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="value">The reference to populate with value.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        public bool TryGetValue(string name, out string? value); 
        // <summary> 
        // Returns header values if the header is stored in the collection. 
        // </summary> 
        // <param name="name">The header name.</param> 
        // <param name="values">The reference to populate with values.</param> 
        // <returns><c>true</c> if the specified header is stored in the collection, otherwise <c>false</c>.</returns> 
        public bool TryGetValues(string name, out IEnumerable<string>? values); 
        // <summary> 
        // Returns an enumerator that iterates through the <see cref="T:Azure.Core.ResponseHeaders" />. 
        // </summary> 
        // <returns>A <see cref="T:System.Collections.IEnumerator" /> for the <see cref="T:Azure.Core.ResponseHeaders" />.</returns> 
        IEnumerator IEnumerable.GetEnumerator(); 
    } 

    // <summary> 
    // The type of approach to apply when calculating the delay 
    // between retry attempts. 
    // </summary> 
    public enum RetryMode { 
        // <summary> 
        // Retry attempts happen at fixed intervals; each delay is a consistent duration. 
        // </summary> 
        Fixed = 0, 
        // <summary> 
        // Retry attempts will delay based on a backoff strategy, where each attempt will increase 
        // the duration that it waits before retrying. 
        // </summary> 
        Exponential = 1, 
    } 

    // <summary> 
    // The set of options that can be specified to influence how 
    // retry attempts are made, and a failure is eligible to be retried. 
    // </summary> 
    public class RetryOptions { 
        // <summary> 
        // The delay between retry attempts for a fixed approach or the delay 
        // on which to base calculations for a backoff-based approach. 
        // If the service provides a Retry-After response header, the next retry will be delayed by the duration specified by the header value. 
        // </summary> 
        public TimeSpan Delay { get; set; }
        // <summary> 
        // The maximum permissible delay between retry attempts when the service does not provide a Retry-After response header. 
        // If the service provides a Retry-After response header, the next retry will be delayed by the duration specified by the header value. 
        // </summary> 
        public TimeSpan MaxDelay { get; set; }
        // <summary> 
        // The maximum number of retry attempts before giving up. 
        // </summary> 
        public int MaxRetries { get; set; }
        // <summary> 
        // The approach to use for calculating retry delays. 
        // </summary> 
        public RetryMode Mode { get; set; }
        // <summary> 
        // The timeout applied to individual network operations. 
        // </summary> 
        public TimeSpan NetworkTimeout { get; set; }
    } 

    // <summary> 
    // This type inherits from ResponseClassifier and is designed to work 
    // efficiently with classifier customizations specified in <see cref="T:Azure.RequestContext" />. 
    // </summary> 
    public class StatusCodeClassifier : ResponseClassifier { 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.StatusCodeClassifier" /> 
        // </summary> 
        // <param name="successStatusCodes">The status codes that this classifier will consider 
        // not to be errors.</param> 
        public StatusCodeClassifier(ReadOnlySpan<ushort> successStatusCodes); 
        // <summary> 
        // Specifies if the response contained in the <paramref name="message" /> is not successful. 
        // </summary> 
        public override bool IsErrorResponse(HttpMessage message); 
    } 

    // <summary> 
    // Represents a method that can handle an event and execute either 
    // synchronously or asynchronously. 
    // </summary> 
    // <typeparam name="T"> 
    // Type of the event arguments deriving or equal to 
    // <see cref="T:Azure.SyncAsyncEventArgs" />. 
    // </typeparam> 
    // <param name="e"> 
    // An <see cref="T:Azure.SyncAsyncEventArgs" /> instance that contains the event 
    // data. 
    // </param> 
    // <returns> 
    // A task that represents the handler.  You can return 
    // <see cref="P:System.Threading.Tasks.Task.CompletedTask" /> if implementing a sync handler. 
    // Please see the Remarks section for more details. 
    // </returns> 
    // <example> 
    // <para> 
    // If you're using the synchronous, blocking methods of a client (i.e., 
    // methods without an Async suffix), they will raise events that require 
    // handlers to execute synchronously as well.  Even though the signature 
    // of your handler returns a <see cref="T:System.Threading.Tasks.Task" />, you should write regular 
    // sync code that blocks and return <see cref="P:System.Threading.Tasks.Task.CompletedTask" /> when 
    // finished. 
    // <code snippet="Snippet:Azure_Core_Samples_EventSamples_SyncHandler" language="csharp"> 
    // var client = new AlarmClient(); 
    // client.Ring += (SyncAsyncEventArgs e) =&gt; 
    // { 
    // Console.WriteLine("Wake up!"); 
    // return Task.CompletedTask; 
    // }; 
    //  
    // client.Snooze(); 
    // </code> 
    // If you need to call an async method from a synchronous event handler, 
    // you have two options.  You can use <see cref="M:System.Threading.Tasks.Task.Run(System.Action)" /> to 
    // queue a task for execution on the ThreadPool without waiting on it to 
    // complete.  This "fire and forget" approach may not run before your 
    // handler finishes executing.  Be sure to understand 
    // <see href="https://docs.microsoft.com/dotnet/standard/parallel-programming/exception-handling-task-parallel-library"> 
    // exception handling in the Task Parallel Library</see> to avoid 
    // unhandled exceptions tearing down your process.  If you absolutely need 
    // the async method to execute before returning from your handler, you can 
    // call <c>myAsyncTask.GetAwaiter().GetResult()</c>.  Please be aware 
    // this may cause ThreadPool starvation.  See the sync-over-async note in 
    // Remarks for more details. 
    // </para> 
    // <para> 
    // If you're using the asynchronous, non-blocking methods of a client 
    // (i.e., methods with an Async suffix), they will raise events that 
    // expect handlers to execute asynchronously. 
    // <code snippet="Snippet:Azure_Core_Samples_EventSamples_AsyncHandler" language="csharp"> 
    // var client = new AlarmClient(); 
    // client.Ring += async (SyncAsyncEventArgs e) =&gt; 
    // { 
    // await Console.Out.WriteLineAsync("Wake up!"); 
    // }; 
    //  
    // await client.SnoozeAsync(); 
    // </code> 
    // </para> 
    // <para> 
    // The same event can be raised from both synchronous and asynchronous 
    // code paths depending on whether you're calling sync or async methods 
    // on a client.  If you write an async handler but raise it from a sync 
    // method, the handler will be doing sync-over-async and may cause 
    // ThreadPool starvation.  See the note in Remarks for more details.  You 
    // should use the <see cref="P:Azure.SyncAsyncEventArgs.IsRunningSynchronously" /> 
    // property to check how the event is being raised and implement your 
    // handler accordingly.  Here's an example handler that's safe to invoke 
    // from both sync and async code paths. 
    // <code snippet="Snippet:Azure_Core_Samples_EventSamples_CombinedHandler" language="csharp"> 
    // var client = new AlarmClient(); 
    // client.Ring += async (SyncAsyncEventArgs e) =&gt; 
    // { 
    // if (e.IsRunningSynchronously) 
    // { 
    // Console.WriteLine("Wake up!"); 
    // } 
    // else 
    // { 
    // await Console.Out.WriteLineAsync("Wake up!"); 
    // } 
    // }; 
    //  
    // client.Snooze(); // sync call that blocks 
    // await client.SnoozeAsync(); // async call that doesn't block 
    // </code> 
    // </para> 
    // </example> 
    // <example> 
    // </example> 
    // <exception cref="T:System.AggregateException"> 
    // Any exceptions thrown by an event handler will be wrapped in a single 
    // AggregateException and thrown from the code that raised the event.  You 
    // can check the <see cref="P:System.AggregateException.InnerExceptions" /> property 
    // to see the original exceptions thrown by your event handlers. 
    // AggregateException also provides 
    // <see href="https://docs.microsoft.com/en-us/archive/msdn-magazine/2009/brownfield/aggregating-exceptions"> 
    // a number of helpful methods</see> like 
    // <see cref="M:System.AggregateException.Flatten" /> and 
    // <see cref="M:System.AggregateException.Handle(System.Func{System.Exception,System.Boolean})" /> to make 
    // complex failures easier to work with. 
    // <code snippet="Snippet:Azure_Core_Samples_EventSamples_Exceptions" language="csharp"> 
    // var client = new AlarmClient(); 
    // client.Ring += (SyncAsyncEventArgs e) =&gt; 
    // throw new InvalidOperationException("Alarm unplugged."); 
    //  
    // try 
    // { 
    // client.Snooze(); 
    // } 
    // catch (AggregateException ex) 
    // { 
    // ex.Handle(e =&gt; e is InvalidOperationException); 
    // Console.WriteLine("Please switch to your backup alarm."); 
    // } 
    // </code> 
    // </exception> 
    // <remarks> 
    // <para> 
    // Most Azure client libraries for .NET offer both synchronous and 
    // asynchronous methods for calling Azure services.  You can distinguish 
    // the asynchronous methods by their Async suffix.  For example, 
    // BlobClient.Download and BlobClient.DownloadAsync make the same 
    // underlying REST call and only differ in whether they block.  We 
    // recommend using our async methods for new applications, but there are 
    // perfectly valid cases for using sync methods as well.  These dual 
    // method invocation semantics allow for flexibility, but require a little 
    // extra care when writing event handlers. 
    // </para> 
    // <para> 
    // The SyncAsyncEventHandler is a delegate used by events in Azure client 
    // libraries to represent an event handler that can be invoked from either 
    // sync or async code paths.  It takes event arguments deriving from 
    // <see cref="T:Azure.SyncAsyncEventArgs" /> that contain important information for 
    // writing your event handler: 
    // <list type="bullet"> 
    // <item> 
    // <description> 
    // <see cref="P:Azure.SyncAsyncEventArgs.CancellationToken" /> is a cancellation 
    // token related to the original operation that raised the event.  It's 
    // important for your handler to pass this token along to any asynchronous 
    // or long-running synchronous operations that take a token so cancellation 
    // (via something like 
    // <c>new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token</c>, 
    // for example) will correctly propagate. 
    // </description> 
    // </item> 
    // <item> 
    // <description> 
    // <see cref="P:Azure.SyncAsyncEventArgs.IsRunningSynchronously" /> is a flag indicating 
    // whether your handler was invoked synchronously or asynchronously.  If 
    // you're calling sync methods on your client, you should use sync methods 
    // to implement your event handler (you can return 
    // <see cref="P:System.Threading.Tasks.Task.CompletedTask" />).  If you're calling async methods on 
    // your client, you should use async methods where possible to implement 
    // your event handler.  If you're not in control of how the client will be 
    // used or want to write safer code, you should check the 
    // <see cref="P:Azure.SyncAsyncEventArgs.IsRunningSynchronously" /> property and call 
    // either sync or async methods as directed. 
    // </description> 
    // </item> 
    // <item> 
    // <description> 
    // Most events will customize the event data by deriving from 
    // <see cref="T:Azure.SyncAsyncEventArgs" /> and including details about what 
    // triggered the event or providing options to react.  Many times this 
    // will include a reference to the client that raised the event in case 
    // you need it for additional processing. 
    // </description> 
    // </item> 
    // </list> 
    // </para> 
    // <para> 
    // When an event using SyncAsyncEventHandler is raised, the handlers will 
    // be executed sequentially to avoid introducing any unintended 
    // parallelism.  The event handlers will finish before returning control 
    // to the code path raising the event.  This means blocking for events 
    // raised synchronously and waiting for the returned <see cref="T:System.Threading.Tasks.Task" /> to 
    // complete for events raised asynchronously. 
    // </para> 
    // <para> 
    // Any exceptions thrown from a handler will be wrapped in a single 
    // <see cref="T:System.AggregateException" />.  If one handler throws an exception, 
    // it will not prevent other handlers from running.  This is also relevant 
    // for cancellation because all handlers are still raised if cancellation 
    // occurs.  You should both pass <see cref="P:Azure.SyncAsyncEventArgs.CancellationToken" /> 
    // to asynchronous or long-running synchronous operations and consider 
    // calling <see cref="M:System.Threading.CancellationToken.ThrowIfCancellationRequested" /> 
    // in compute heavy handlers. 
    // </para> 
    // <para> 
    // A <see href="https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/samples/Diagnostics.md#distributed-tracing"> 
    // distributed tracing span</see> is wrapped around your handlers using 
    // the event name so you can see how long your handlers took to run, 
    // whether they made other calls to Azure services, and details about any 
    // exceptions that were thrown. 
    // </para> 
    // <para> 
    // Executing asynchronous code from a sync code path is commonly referred 
    // to as sync-over-async because you're getting sync behavior but still 
    // invoking all the async machinery. See 
    // <see href="https://docs.microsoft.com/archive/blogs/vancem/diagnosing-net-core-threadpool-starvation-with-perfview-why-my-service-is-not-saturating-all-cores-or-seems-to-stall"> 
    // Diagnosing.NET Core ThreadPool Starvation with PerfView</see> 
    // for a detailed explanation of how that can cause serious performance 
    // problems.  We recommend you use the 
    // <see cref="P:Azure.SyncAsyncEventArgs.IsRunningSynchronously" /> flag to avoid 
    // ThreadPool starvation. 
    // </para> 
    // </remarks> 
    public delegate Task SyncAsyncEventHandler<T>(T e) where T : SyncAsyncEventArgs; 
    // <summary> 
    // Details about the package to be included in UserAgent telemetry 
    // </summary> 
    public class TelemetryDetails { 
        // <summary> 
        // Initialize an instance of <see cref="T:Azure.Core.TelemetryDetails" /> by extracting the name and version information from the <see cref="T:System.Reflection.Assembly" /> associated with the <paramref name="assembly" />. 
        // </summary> 
        // <param name="assembly">The <see cref="T:System.Reflection.Assembly" /> used to generate the package name and version information for the <see cref="T:Azure.Core.TelemetryDetails" /> value.</param> 
        // <param name="applicationId">An optional value to be prepended to the <see cref="T:Azure.Core.TelemetryDetails" />. 
        // This value overrides the behavior of the <see cref="P:Azure.Core.DiagnosticsOptions.ApplicationId" /> property for the <see cref="T:Azure.Core.HttpMessage" /> it is applied to.</param> 
        public TelemetryDetails(Assembly assembly, string? applicationId = null); 
        // <summary> 
        // The value of the applicationId used to initialize this <see cref="T:Azure.Core.TelemetryDetails" /> instance. 
        // </summary> 
        public string? ApplicationId { get; }
        // <summary> 
        // The package type represented by this <see cref="T:Azure.Core.TelemetryDetails" /> instance. 
        // </summary> 
        public Assembly Assembly { get; }
        // <summary> 
        // Sets the package name and version portion of the UserAgent telemetry value for the context of the <paramref name="message" /> 
        // Note: If <see cref="P:Azure.Core.DiagnosticsOptions.IsTelemetryEnabled" /> is false, this value is never used. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> that will use this <see cref="T:Azure.Core.TelemetryDetails" />.</param> 
        public void Apply(HttpMessage message); 
        // <summary> 
        // The properly formatted UserAgent string based on this <see cref="T:Azure.Core.TelemetryDetails" /> instance. 
        // </summary> 
        public override string ToString(); 
    } 

    // <summary> 
    // Represents a credential capable of providing an OAuth token. 
    // </summary> 
    public abstract class TokenCredential : AuthenticationTokenProvider { 
        protected TokenCredential(); 
        // <summary> 
        // Creates a new instance of <see cref="T:System.ClientModel.Primitives.GetTokenOptions" /> using the provided <paramref name="properties" />. 
        // </summary><param name="properties"></param><returns>An instance of <see cref="T:System.ClientModel.Primitives.GetTokenOptions" /> or <c>null</c> if the provided options are not valid.</returns> 
        public override GetTokenOptions? CreateTokenOptions(IReadOnlyDictionary<string, object> properties); 
        // <summary> 
        // Gets an <see cref="T:Azure.Core.AccessToken" /> for the specified set of scopes. 
        // </summary> 
        // <param name="requestContext">The <see cref="T:Azure.Core.TokenRequestContext" /> with authentication information.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use.</param> 
        // <returns>A valid <see cref="T:Azure.Core.AccessToken" />.</returns> 
        // <remarks>Caching and management of the lifespan for the <see cref="T:Azure.Core.AccessToken" /> is considered the responsibility of the caller: each call should request a fresh token being requested.</remarks> 
        public abstract AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken); 
        // <summary> 
        // Gets an <see cref="T:System.ClientModel.Primitives.AuthenticationToken" /> for the provided <paramref name="properties" />. 
        // </summary> 
        // <param name="properties"></param> 
        // <param name="cancellationToken"></param> 
        // <returns></returns> 
        public override AuthenticationToken GetToken(GetTokenOptions properties, CancellationToken cancellationToken); 
        // <summary> 
        // Gets an <see cref="T:Azure.Core.AccessToken" /> for the specified set of scopes. 
        // </summary> 
        // <param name="requestContext">The <see cref="T:Azure.Core.TokenRequestContext" /> with authentication information.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use.</param> 
        // <returns>A valid <see cref="T:Azure.Core.AccessToken" />.</returns> 
        // <remarks>Caching and management of the lifespan for the <see cref="T:Azure.Core.AccessToken" /> is considered the responsibility of the caller: each call should request a fresh token being requested.</remarks> 
        public abstract ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken); 
        // <summary> 
        // Gets an <see cref="T:System.ClientModel.Primitives.AuthenticationToken" /> for the provided <paramref name="properties" />. 
        // </summary> 
        // <param name="properties"></param> 
        // <param name="cancellationToken"></param> 
        // <returns></returns> 
        // <exception cref="T:System.NotImplementedException"></exception> 
        public override ValueTask<AuthenticationToken> GetTokenAsync(GetTokenOptions properties, CancellationToken cancellationToken); 
    } 

    // <summary> 
    // Contains the details of an authentication token request. 
    // </summary> 
    public readonly struct TokenRequestContext { 
        // <summary> 
        // Creates a new TokenRequest with the specified scopes. 
        // </summary> 
        // <param name="scopes">The scopes required for the token.</param> 
        // <param name="parentRequestId">The <see cref="P:Azure.Core.Request.ClientRequestId" /> of the request requiring a token for authentication, if applicable.</param> 
        public TokenRequestContext(string[] scopes, string? parentRequestId); 
        // <summary> 
        // Creates a new TokenRequest with the specified scopes. 
        // </summary> 
        // <param name="scopes">The scopes required for the token.</param> 
        // <param name="parentRequestId">The <see cref="P:Azure.Core.Request.ClientRequestId" /> of the request requiring a token for authentication, if applicable.</param> 
        // <param name="claims">Additional claims to be included in the token.</param> 
        public TokenRequestContext(string[] scopes, string? parentRequestId, string? claims); 
        // <summary> 
        // Creates a new TokenRequest with the specified scopes. 
        // </summary> 
        // <param name="scopes">The scopes required for the token.</param> 
        // <param name="parentRequestId">The <see cref="P:Azure.Core.Request.ClientRequestId" /> of the request requiring a token for authentication, if applicable.</param> 
        // <param name="claims">Additional claims to be included in the token.</param> 
        // <param name="tenantId"> The tenantId to be included in the token request. </param> 
        public TokenRequestContext(string[] scopes, string? parentRequestId, string? claims, string? tenantId); 
        // <summary> 
        // Creates a new TokenRequest with the specified scopes. 
        // </summary> 
        // <param name="scopes">The scopes required for the token.</param> 
        // <param name="parentRequestId">The <see cref="P:Azure.Core.Request.ClientRequestId" /> of the request requiring a token for authentication, if applicable.</param> 
        // <param name="claims">Additional claims to be included in the token.</param> 
        // <param name="tenantId"> The tenantId to be included in the token request.</param> 
        // <param name="isCaeEnabled">Indicates whether to enable Continuous Access Evaluation (CAE) for the requested token.</param> 
        public TokenRequestContext(string[] scopes, string? parentRequestId, string? claims, string? tenantId, bool isCaeEnabled); 
        // <summary> 
        // Creates a new TokenRequest with the specified scopes. 
        // </summary> 
        // <param name="scopes">The scopes required for the token.</param> 
        // <param name="parentRequestId">The <see cref="P:Azure.Core.Request.ClientRequestId" /> of the request requiring a token for authentication, if applicable.</param> 
        // <param name="claims">Additional claims to be included in the token.</param> 
        // <param name="tenantId">The tenant ID to be included in the token request.</param> 
        // <param name="isCaeEnabled">Indicates whether to enable Continuous Access Evaluation (CAE) for the requested token.</param> 
        // <param name="isProofOfPossessionEnabled">Indicates whether to enable Proof of Possession (PoP) for the requested token.</param> 
        // <param name="proofOfPossessionNonce">The nonce value required for PoP token requests.</param> 
        // <param name="requestUri">The resource request URI to be authorized with a PoP token.</param> 
        // <param name="requestMethod">The HTTP request method name of the resource request (e.g. GET, POST, etc.).</param> 
        public TokenRequestContext(string[] scopes, string? parentRequestId = null, string? claims = null, string? tenantId = null, bool isCaeEnabled = false, bool isProofOfPossessionEnabled = false, string? proofOfPossessionNonce = null, Uri? requestUri = null, string? requestMethod = null); 
        // <summary> 
        // Additional claims to be included in the token. See <see href="https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter">https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter</see> for more information on format and content. 
        // </summary> 
        public string? Claims { get; }
        // <summary> 
        // Indicates whether to enable Continuous Access Evaluation (CAE) for the requested token. 
        // </summary> 
        // <remarks> 
        // If a resource API implements CAE and your application declares it can handle CAE, your app receives CAE tokens for that resource. 
        // For this reason, if you declare your app CAE ready, your application must handle the CAE claim challenge for all resource APIs that accept Microsoft Identity access tokens. 
        // If you don't handle CAE responses in these API calls, your app could end up in a loop retrying an API call with a token that is still in the returned lifespan of the token but has been revoked due to CAE. 
        // </remarks> 
        public bool IsCaeEnabled { get; }
        // <summary> 
        // Indicates whether to enable Proof of Possession (PoP) for the requested token. 
        // </summary> 
        public bool IsProofOfPossessionEnabled { get; }
        // <summary> 
        // The <see cref="P:Azure.Core.Request.ClientRequestId" /> of the request requiring a token for authentication, if applicable. 
        // </summary> 
        public string? ParentRequestId { get; }
        // <summary> 
        // The nonce value required for PoP token requests. This is typically retrieved from the WWW-Authenticate header of a 401 challenge response. 
        // This is used in combination with <see cref="P:Azure.Core.TokenRequestContext.ResourceRequestUri" /> and <see cref="P:Azure.Core.TokenRequestContext.ResourceRequestMethod" /> to generate the PoP token. 
        // </summary> 
        public string? ProofOfPossessionNonce { get; }
        // <summary> 
        // The HTTP method of the request. This is used in combination with <see cref="P:Azure.Core.TokenRequestContext.ResourceRequestUri" /> and <see cref="P:Azure.Core.TokenRequestContext.ProofOfPossessionNonce" /> to generate the PoP token. 
        // </summary> 
        public string? ResourceRequestMethod { get; }
        // <summary> 
        // The URI of the request. This is used in combination with <see cref="P:Azure.Core.TokenRequestContext.ResourceRequestMethod" /> and <see cref="P:Azure.Core.TokenRequestContext.ProofOfPossessionNonce" /> to generate the PoP token. 
        // </summary> 
        public Uri? ResourceRequestUri { get; }
        // <summary> 
        // The scopes required for the token. 
        // </summary> 
        public string[] Scopes { get; }
        // <summary> 
        // The tenantId to be included in the token request. 
        // </summary> 
        public string? TenantId { get; }
    } 

} 

namespace Azure.Core.Cryptography { 
    // <summary> 
    // A key which is used to encrypt, or wrap, another key. 
    // </summary> 
    public interface IKeyEncryptionKey { 
        // <summary> 
        // The Id of the key used to perform cryptographic operations for the client. 
        // </summary> 
        string KeyId { get; }
        // <summary> 
        // Decrypts the specified encrypted key using the specified algorithm. 
        // </summary> 
        // <param name="algorithm">The key wrap algorithm which was used to encrypt the specified encrypted key.</param> 
        // <param name="encryptedKey">The encrypted key to be decrypted.</param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> controlling the request lifetime.</param> 
        // <returns>The decrypted key bytes.</returns> 
        byte[] UnwrapKey(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default); 
        // <summary> 
        // Decrypts the specified encrypted key using the specified algorithm. 
        // </summary> 
        // <param name="algorithm">The key wrap algorithm which was used to encrypt the specified encrypted key.</param> 
        // <param name="encryptedKey">The encrypted key to be decrypted.</param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> controlling the request lifetime.</param> 
        // <returns>The decrypted key bytes.</returns> 
        Task<byte[]> UnwrapKeyAsync(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default); 
        // <summary> 
        // Encrypts the specified key using the specified algorithm. 
        // </summary> 
        // <param name="algorithm">The key wrap algorithm used to encrypt the specified key.</param> 
        // <param name="key">The key to be encrypted.</param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> controlling the request lifetime.</param> 
        // <returns>The encrypted key bytes.</returns> 
        byte[] WrapKey(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default); 
        // <summary> 
        // Encrypts the specified key using the specified algorithm. 
        // </summary> 
        // <param name="algorithm">The key wrap algorithm used to encrypt the specified key.</param> 
        // <param name="key">The key to be encrypted.</param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> controlling the request lifetime.</param> 
        // <returns>The encrypted key bytes.</returns> 
        Task<byte[]> WrapKeyAsync(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default); 
    } 

    // <summary> 
    // An object capable of retrieving key encryption keys from a provided key identifier. 
    // </summary> 
    public interface IKeyEncryptionKeyResolver { 
        // <summary> 
        // Retrieves the key encryption key corresponding to the specified keyId. 
        // </summary> 
        // <param name="keyId">The key identifier of the key encryption key to retrieve.</param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> controlling the request lifetime.</param> 
        // <returns>The key encryption key corresponding to the specified keyId.</returns> 
        IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default); 
        // <summary> 
        // Retrieves the key encryption key corresponding to the specified keyId. 
        // </summary> 
        // <param name="keyId">The key identifier of the key encryption key to retrieve.</param> 
        // <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> controlling the request lifetime.</param> 
        // <returns>The key encryption key corresponding to the specified keyId.</returns> 
        Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default); 
    } 

} 

namespace Azure.Core.Diagnostics { 
    // <summary> 
    // Implementation of <see cref="T:System.Diagnostics.Tracing.EventListener" /> that listens to events produced by Azure SDK client libraries. 
    // </summary> 
    public class AzureEventSourceListener : EventListener { 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.Diagnostics.AzureEventSourceListener" /> that executes a <paramref name="log" /> callback every time event is written. 
        // </summary> 
        // <param name="log">The <see cref="T:System.Action`1" /> to call when event is written.</param> 
        // <param name="level">The level of events to enable.</param> 
        public AzureEventSourceListener(Action<EventWrittenEventArgs> log, EventLevel level); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.Diagnostics.AzureEventSourceListener" /> that executes a <paramref name="log" /> callback every time event is written. 
        // </summary> 
        // <param name="log">The <see cref="T:System.Action`2" /> to call when event is written. The second parameter is the formatted message.</param> 
        // <param name="level">The level of events to enable.</param> 
        public AzureEventSourceListener(Action<EventWrittenEventArgs, string> log, EventLevel level); 
        // <summary> 
        // The trait name that has to be present on all event sources collected by this listener. 
        // </summary> 
        public const string TraitName = "AzureEventSource"; 
        // <summary> 
        // The trait value that has to be present on all event sources collected by this listener. 
        // </summary> 
        public const string TraitValue = "true"; 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.Diagnostics.AzureEventSourceListener" /> that forwards events to <see cref="M:System.Console.WriteLine(System.String)" />. 
        // </summary> 
        // <param name="level">The level of events to enable.</param> 
        public static AzureEventSourceListener CreateConsoleLogger(EventLevel level = Informational); 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.Diagnostics.AzureEventSourceListener" /> that forwards events to <see cref="M:System.Diagnostics.Trace.WriteLine(System.Object)" />. 
        // </summary> 
        // <param name="level">The level of events to enable.</param> 
        public static AzureEventSourceListener CreateTraceLogger(EventLevel level = Informational); 
        // <summary>Called for all existing event sources when the event listener is created and when a new event source is attached to the listener.</summary><param name="eventSource">The event source.</param> 
        protected override sealed void OnEventSourceCreated(EventSource eventSource); 
        // <summary>Called whenever an event has been written by an event source for which the event listener has enabled events.</summary><param name="eventData">The event arguments that describe the event.</param> 
        protected override sealed void OnEventWritten(EventWrittenEventArgs eventData); 
    } 

} 

namespace Azure.Core.Extensions { 
    // <summary> 
    // Marks the type exposing client registration options for clients registered with <see cref="T:Azure.Core.Extensions.IAzureClientFactoryBuilder" />. 
    // </summary> 
    // <typeparam name="TClient">The type of the client.</typeparam> 
    // <typeparam name="TOptions">The options type used by the client.</typeparam> 
    public interface IAzureClientBuilder<TClient, TOptions> where TOptions : class { 
    } 

    // <summary> 
    // Abstraction for registering Azure clients in dependency injection containers. 
    // </summary> 
    public interface IAzureClientFactoryBuilder { 
        // <summary> 
        // Registers a client in the dependency injection container using the factory to create a client instance. 
        // </summary> 
        // <typeparam name="TClient">The type of the client.</typeparam> 
        // <typeparam name="TOptions">The client options type used the client.</typeparam> 
        // <param name="clientFactory">The factory, that given the instance of options, returns a client instance.</param> 
        // <returns><see cref="T:Azure.Core.Extensions.IAzureClientBuilder`2" /> that allows customizing the client registration.</returns> 
        IAzureClientBuilder<TClient, TOptions> RegisterClientFactory<TClient, TOptions>(Func<TOptions, TClient> clientFactory) where TOptions : class; 
    } 

    // <summary> 
    // Abstraction for registering Azure clients in dependency injection containers and initializing them using <c>IConfiguration</c> objects. 
    // </summary> 
    public interface IAzureClientFactoryBuilderWithConfiguration<in TConfiguration> : IAzureClientFactoryBuilder { 
        // <summary> 
        // Registers a client in the dependency injection container using the configuration to create a client instance. 
        // </summary> 
        // <typeparam name="TClient">The type of the client.</typeparam> 
        // <typeparam name="TOptions">The client options type used the client.</typeparam> 
        // <param name="configuration">Instance of <typeparamref name="TConfiguration" /> to use.</param> 
        // <returns><see cref="T:Azure.Core.Extensions.IAzureClientBuilder`2" /> that allows customizing the client registration.</returns> 
        IAzureClientBuilder<TClient, TOptions> RegisterClientFactory<TClient, TOptions>(TConfiguration configuration) where TOptions : class; 
    } 

    // <summary> 
    // Abstraction for registering Azure clients that require <see cref="T:Azure.Core.TokenCredential" /> in dependency injection containers. 
    // </summary> 
    public interface IAzureClientFactoryBuilderWithCredential { 
        // <summary> 
        // Registers a client in dependency injection container the using the factory to create a client instance. 
        // </summary> 
        // <typeparam name="TClient">The type of the client.</typeparam> 
        // <typeparam name="TOptions">The client options type used the client.</typeparam> 
        // <param name="clientFactory">The factory, that given the instance of options and credential, returns a client instance.</param> 
        // <param name="requiresCredential">Specifies whether the credential is optional (client supports anonymous authentication).</param> 
        // <returns><see cref="T:Azure.Core.Extensions.IAzureClientBuilder`2" /> that allows customizing the client registration.</returns> 
        IAzureClientBuilder<TClient, TOptions> RegisterClientFactory<TClient, TOptions>(Func<TOptions, TokenCredential, TClient> clientFactory, bool requiresCredential = true) where TOptions : class; 
    } 

} 

namespace Azure.Core.GeoJson { 
    // <summary> 
    // Represents a geometry coordinates array 
    // </summary> 
    // <typeparam name="T">The type of the value.</typeparam> 
    public readonly struct GeoArray<T> : IReadOnlyList<T>, IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable { 
        // <summary> 
        // Enumerates the elements of a <see cref="T:Azure.Core.GeoJson.GeoArray`1" /> 
        // </summary> 
        public struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator { 
            // <summary>Gets the element in the collection at the current position of the enumerator.</summary><returns>The element in the collection at the current position of the enumerator.</returns> 
            public T Current { get; }
            // <summary>Gets the current element in the collection.</summary><returns>The current element in the collection.</returns> 
            object IEnumerator.Current { get; }
            // <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary> 
            public void Dispose(); 
            // <summary>Advances the enumerator to the next element of the collection.</summary><returns>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.</returns><exception cref="T:System.InvalidOperationException">The collection was modified after the enumerator was created. </exception> 
            public bool MoveNext(); 
            // <summary>Sets the enumerator to its initial position, which is before the first element in the collection.</summary><exception cref="T:System.InvalidOperationException">The collection was modified after the enumerator was created. </exception> 
            public void Reset(); 
        } 

        // <summary> 
        // Returns the size of the array. 
        // </summary> 
        public int Count { get; }
        // <summary> 
        // Returns a value at the provided index. 
        // </summary> 
        // <param name="index">The index to retrieve the value from.</param> 
        public T this[int index] { get; }
        // <summary> 
        // Returns an enumerator that iterates through the collection. 
        // </summary> 
        // <returns>An enumerator that can be used to iterate through the collection.</returns> 
        public Enumerator GetEnumerator(); 
        // <summary>Returns an enumerator that iterates through the collection.</summary><returns>An enumerator that can be used to iterate through the collection.</returns> 
        IEnumerator<T> IEnumerable<T>.GetEnumerator(); 
        // <summary>Returns an enumerator that iterates through a collection.</summary><returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns> 
        IEnumerator IEnumerable.GetEnumerator(); 
    } 

    // <summary> 
    // Represents information about the coordinate range of the <see cref="T:Azure.Core.GeoJson.GeoObject" />. 
    // </summary> 
    public sealed class GeoBoundingBox : IEquatable<GeoBoundingBox> { 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" />. 
        // </summary> 
        public GeoBoundingBox(double west, double south, double east, double north); 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" />. 
        // </summary> 
        public GeoBoundingBox(double west, double south, double east, double north, double? minAltitude, double? maxAltitude); 
        // <summary> 
        // The eastmost value of <see cref="T:Azure.Core.GeoJson.GeoObject" /> coordinates. 
        // </summary> 
        public double East { get; }
        // <summary> 
        // The maximum altitude value of <see cref="T:Azure.Core.GeoJson.GeoObject" /> coordinates. 
        // </summary> 
        public double? MaxAltitude { get; }
        // <summary> 
        // The minimum altitude value of <see cref="T:Azure.Core.GeoJson.GeoObject" /> coordinates. 
        // </summary> 
        public double? MinAltitude { get; }
        // <summary> 
        // The northmost value of <see cref="T:Azure.Core.GeoJson.GeoObject" /> coordinates. 
        // </summary> 
        public double North { get; }
        // <summary> 
        // The southmost value of <see cref="T:Azure.Core.GeoJson.GeoObject" /> coordinates. 
        // </summary> 
        public double South { get; }
        // <summary> 
        // Gets the component of the <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" /> based on its index. 
        // </summary> 
        // <param name="index">The index of the bounding box component.</param> 
        public double this[int index] { get; }
        // <summary> 
        // The westmost value of <see cref="T:Azure.Core.GeoJson.GeoObject" /> coordinates. 
        // </summary> 
        public double West { get; }
        // <summary>Indicates whether the current object is equal to another object of the same type.</summary><param name="other">An object to compare with this object.</param><returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns> 
        public bool Equals(GeoBoundingBox? other); 
        // <summary>Determines whether the specified object is equal to the current object.</summary><param name="obj">The object to compare with the current object. </param><returns>true if the specified object  is equal to the current object; otherwise, false.</returns> 
        public override bool Equals(object? obj); 
        // <summary>Serves as the default hash function. </summary><returns>A hash code for the current object.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns a string that represents the current object.</summary><returns>A string that represents the current object.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Represents a geometry that is composed of multiple geometries. 
    // </summary> 
    [JsonConverter(typeof(GeoJsonConverter))] 
    public sealed class GeoCollection : GeoObject, IReadOnlyList<GeoObject>, IReadOnlyCollection<GeoObject>, IEnumerable<GeoObject>, IEnumerable { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoCollection" />. 
        // </summary> 
        // <param name="geometries">The collection of inner geometries.</param> 
        public GeoCollection(IEnumerable<GeoObject> geometries); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoCollection" />. 
        // </summary> 
        // <param name="geometries">The collection of inner geometries.</param> 
        // <param name="boundingBox">The <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" /> to use.</param> 
        // <param name="customProperties">The set of custom properties associated with the <see cref="T:Azure.Core.GeoJson.GeoObject" />.</param> 
        public GeoCollection(IEnumerable<GeoObject> geometries, GeoBoundingBox? boundingBox, IReadOnlyDictionary<string, object?> customProperties); 
        // <summary>Gets the number of elements in the collection.</summary><returns>The number of elements in the collection. </returns> 
        public int Count { get; }
        // <summary>Gets the element at the specified index in the read-only list.</summary><param name="index">The zero-based index of the element to get. </param><returns>The element at the specified index in the read-only list.</returns> 
        public GeoObject this[int index] { get; }
        // <summary> 
        // Gets the GeoJSON type of this object. 
        // </summary> 
        public override GeoObjectType Type { get; }
        // <summary>Returns an enumerator that iterates through the collection.</summary><returns>An enumerator that can be used to iterate through the collection.</returns> 
        public IEnumerator<GeoObject> GetEnumerator(); 
        // <summary>Returns an enumerator that iterates through a collection.</summary><returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns> 
        IEnumerator IEnumerable.GetEnumerator(); 
    } 

    // <summary> 
    // Represents a linear ring that's a part of a polygon 
    // </summary> 
    public sealed class GeoLinearRing { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoLinearRing" />. 
        // </summary> 
        // <param name="coordinates"></param> 
        public GeoLinearRing(IEnumerable<GeoPosition> coordinates); 
        // <summary> 
        // Returns a view over the coordinates array that forms this linear ring. 
        // </summary> 
        public GeoArray<GeoPosition> Coordinates { get; }
    } 

    // <summary> 
    // Represents a line geometry that consists of multiple coordinates. 
    // </summary> 
    // <example> 
    // Creating a line: 
    // <code snippet="Snippet:CreateLineString" language="csharp"> 
    // var line = new GeoLineString(new[] 
    // { 
    // new GeoPosition(-122.108727, 47.649383), 
    // new GeoPosition(-122.081538, 47.640846), 
    // new GeoPosition(-122.078634, 47.576066), 
    // new GeoPosition(-122.112686, 47.578559), 
    // }); 
    // </code> 
    // </example> 
    [JsonConverter(typeof(GeoJsonConverter))] 
    public sealed class GeoLineString : GeoObject { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoLineString" />. 
        // </summary> 
        // <param name="coordinates">The collection of <see cref="T:Azure.Core.GeoJson.GeoPosition" /> that make up the line.</param> 
        public GeoLineString(IEnumerable<GeoPosition> coordinates); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoLineString" />. 
        // </summary> 
        // <param name="coordinates">The collection of <see cref="T:Azure.Core.GeoJson.GeoPosition" /> that make up the line.</param> 
        // <param name="boundingBox">The <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" /> to use.</param> 
        // <param name="customProperties">The set of custom properties associated with the <see cref="T:Azure.Core.GeoJson.GeoObject" />.</param> 
        public GeoLineString(IEnumerable<GeoPosition> coordinates, GeoBoundingBox? boundingBox, IReadOnlyDictionary<string, object?> customProperties); 
        // <summary> 
        // Returns a view over the coordinates array that forms this geometry. 
        // </summary> 
        public GeoArray<GeoPosition> Coordinates { get; }
        // <summary> 
        // Gets the GeoJSON type of this object. 
        // </summary> 
        public override GeoObjectType Type { get; }
    } 

    // <summary> 
    // Represents a geometry that is composed of multiple <see cref="T:Azure.Core.GeoJson.GeoLineString" />. 
    // </summary> 
    [JsonConverter(typeof(GeoJsonConverter))] 
    public sealed class GeoLineStringCollection : GeoObject, IReadOnlyList<GeoLineString>, IReadOnlyCollection<GeoLineString>, IEnumerable<GeoLineString>, IEnumerable { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoLineStringCollection" />. 
        // </summary> 
        // <param name="lines">The collection of inner lines.</param> 
        public GeoLineStringCollection(IEnumerable<GeoLineString> lines); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoLineStringCollection" />. 
        // </summary> 
        // <param name="lines">The collection of inner lines.</param> 
        // <param name="boundingBox">The <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" /> to use.</param> 
        // <param name="customProperties">The set of custom properties associated with the <see cref="T:Azure.Core.GeoJson.GeoObject" />.</param> 
        public GeoLineStringCollection(IEnumerable<GeoLineString> lines, GeoBoundingBox? boundingBox, IReadOnlyDictionary<string, object?> customProperties); 
        // <summary> 
        // Returns a view over the coordinates array that forms this geometry. 
        // </summary> 
        public GeoArray<GeoArray<GeoPosition>> Coordinates { get; }
        // <summary>Gets the number of elements in the collection.</summary><returns>The number of elements in the collection. </returns> 
        public int Count { get; }
        // <summary>Gets the element at the specified index in the read-only list.</summary><param name="index">The zero-based index of the element to get. </param><returns>The element at the specified index in the read-only list.</returns> 
        public GeoLineString this[int index] { get; }
        // <summary> 
        // Gets the GeoJSON type of this object. 
        // </summary> 
        public override GeoObjectType Type { get; }
        // <summary>Returns an enumerator that iterates through the collection.</summary><returns>An enumerator that can be used to iterate through the collection.</returns> 
        public IEnumerator<GeoLineString> GetEnumerator(); 
        // <summary>Returns an enumerator that iterates through a collection.</summary><returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns> 
        IEnumerator IEnumerable.GetEnumerator(); 
    } 

    // <summary> 
    // A base type for all spatial types. 
    // </summary> 
    [JsonConverter(typeof(GeoJsonConverter))] 
    public abstract class GeoObject { 
        // <summary> 
        // Represents information about the coordinate range of the <see cref="T:Azure.Core.GeoJson.GeoObject" />. 
        // </summary> 
        public GeoBoundingBox? BoundingBox { get; }
        // <summary> 
        // Gets the GeoJSON type of this object. 
        // </summary> 
        public abstract GeoObjectType Type { get; }
        // <summary> 
        // Parses an instance of see <see cref="T:Azure.Core.GeoJson.GeoObject" /> from provided JSON representation. 
        // </summary> 
        // <param name="json">The GeoJSON representation of an object.</param> 
        // <returns>The resulting <see cref="T:Azure.Core.GeoJson.GeoObject" /> object.</returns> 
        public static GeoObject Parse(string json); 
        // <summary> 
        // Tries to get a value of a custom property associated with the <see cref="T:Azure.Core.GeoJson.GeoObject" />. 
        // </summary> 
        public bool TryGetCustomProperty(string name, out object? value); 
        // <summary> 
        // Converts an instance of <see cref="T:Azure.Core.GeoJson.GeoObject" /> to a GeoJSON representation. 
        // </summary> 
        // <returns></returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Identifies the type of the <see cref="T:Azure.Core.GeoJson.GeoObject" /> 
    // </summary> 
    public enum GeoObjectType { 
        // <summary> 
        // The <see cref="T:Azure.Core.GeoJson.GeoObject" /> is of the <see cref="T:Azure.Core.GeoJson.GeoPoint" /> type. 
        // </summary> 
        Point = 0, 
        // <summary> 
        // The <see cref="T:Azure.Core.GeoJson.GeoObject" /> is of the <see cref="T:Azure.Core.GeoJson.GeoPointCollection" /> type. 
        // </summary> 
        MultiPoint = 1, 
        // <summary> 
        // The <see cref="T:Azure.Core.GeoJson.GeoObject" /> is of the <see cref="T:Azure.Core.GeoJson.GeoPolygon" /> type. 
        // </summary> 
        Polygon = 2, 
        // <summary> 
        // The <see cref="T:Azure.Core.GeoJson.GeoObject" /> is of the <see cref="T:Azure.Core.GeoJson.GeoPolygonCollection" /> type. 
        // </summary> 
        MultiPolygon = 3, 
        // <summary> 
        // The <see cref="T:Azure.Core.GeoJson.GeoObject" /> is of the <see cref="T:Azure.Core.GeoJson.GeoLineString" /> type. 
        // </summary> 
        LineString = 4, 
        // <summary> 
        // The <see cref="T:Azure.Core.GeoJson.GeoObject" /> is of the <see cref="T:Azure.Core.GeoJson.GeoLineStringCollection" /> type. 
        // </summary> 
        MultiLineString = 5, 
        // <summary> 
        // The <see cref="T:Azure.Core.GeoJson.GeoObject" /> is of the <see cref="T:Azure.Core.GeoJson.GeoCollection" /> type. 
        // </summary> 
        GeometryCollection = 6, 
    } 

    // <summary> 
    // Represents a point geometry. 
    // </summary> 
    // <example> 
    // Creating a point: 
    // <code snippet="Snippet:CreatePoint" language="csharp"> 
    // var point = new GeoPoint(-122.091954, 47.607148); 
    // </code> 
    // </example> 
    // <summary> 
    // Represents a point geometry. 
    // </summary> 
    // <example> 
    // Creating a point: 
    // <code snippet="Snippet:CreatePoint" language="csharp"> 
    // var point = new GeoPoint(-122.091954, 47.607148); 
    // </code> 
    // </example> 
    [JsonConverter(typeof(GeoJsonConverter))] 
    public sealed class GeoPoint : GeoObject, IJsonModel<GeoPoint>, IPersistableModel<GeoPoint> { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPoint" />. 
        // </summary> 
        public GeoPoint(); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPoint" />. 
        // </summary> 
        // <param name="longitude">The longitude of the point.</param> 
        // <param name="latitude">The latitude of the point.</param> 
        public GeoPoint(double longitude, double latitude); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPoint" />. 
        // </summary> 
        // <param name="longitude">The longitude of the point.</param> 
        // <param name="latitude">The latitude of the point.</param> 
        // <param name="altitude">The altitude of the point.</param> 
        public GeoPoint(double longitude, double latitude, double? altitude); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPoint" />. 
        // </summary> 
        // <param name="position">The position of the point.</param> 
        public GeoPoint(GeoPosition position); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPoint" />. 
        // </summary> 
        // <param name="position">The position of the point.</param> 
        // <param name="boundingBox">The <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" /> to use.</param> 
        // <param name="customProperties">The set of custom properties associated with the <see cref="T:Azure.Core.GeoJson.GeoObject" />.</param> 
        public GeoPoint(GeoPosition position, GeoBoundingBox? boundingBox, IReadOnlyDictionary<string, object?> customProperties); 
        // <summary> 
        // Gets position of the point. 
        // </summary> 
        public GeoPosition Coordinates { get; }
        // <summary> 
        // Gets the GeoJSON type of this object. 
        // </summary> 
        public override GeoObjectType Type { get; }
        // <summary> 
        // Reads one JSON value (including objects or arrays) from the provided reader and converts it to a model. 
        // </summary><param name="reader">The <see cref="T:System.Text.Json.Utf8JsonReader" /> to read.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A <typeparamref name="T" /> representation of the JSON value.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        GeoPoint? IJsonModel<GeoPoint>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options); 
        // <summary> 
        // Writes the model to the provided <see cref="T:System.Text.Json.Utf8JsonWriter" />. 
        // </summary><param name="writer">The <see cref="T:System.Text.Json.Utf8JsonWriter" /> to write into.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        void IJsonModel<GeoPoint>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options); 
        // <summary> 
        // Converts the provided <see cref="T:System.BinaryData" /> into a model. 
        // </summary><param name="data">The <see cref="T:System.BinaryData" /> to parse.</param><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A <typeparamref name="T" /> representation of the data.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        GeoPoint? IPersistableModel<GeoPoint>.Create(BinaryData data, ModelReaderWriterOptions options); 
        // <summary> 
        // Gets the data interchange format (JSON, Xml, etc) that the model uses when communicating with the service. 
        // </summary><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to consider when serializing and deserializing the model.</param><returns>The format that the model uses when communicating with the service.</returns> 
        string IPersistableModel<GeoPoint>.GetFormatFromOptions(ModelReaderWriterOptions options); 
        // <summary> 
        // Writes the model into a <see cref="T:System.BinaryData" />. 
        // </summary><param name="options">The <see cref="T:System.ClientModel.Primitives.ModelReaderWriterOptions" /> to use.</param><returns>A binary representation of the written model.</returns><exception cref="T:System.FormatException">If the model does not support the requested <see cref="P:System.ClientModel.Primitives.ModelReaderWriterOptions.Format" />.</exception> 
        BinaryData IPersistableModel<GeoPoint>.Write(ModelReaderWriterOptions options); 
    } 

    // <summary> 
    // Represents a geometry that is composed of multiple <see cref="T:Azure.Core.GeoJson.GeoPoint" />. 
    // </summary> 
    [JsonConverter(typeof(GeoJsonConverter))] 
    public sealed class GeoPointCollection : GeoObject, IReadOnlyList<GeoPoint>, IReadOnlyCollection<GeoPoint>, IEnumerable<GeoPoint>, IEnumerable { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPointCollection" />. 
        // </summary> 
        // <param name="points">The collection of inner points.</param> 
        public GeoPointCollection(IEnumerable<GeoPoint> points); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPointCollection" />. 
        // </summary> 
        // <param name="points">The collection of inner points.</param> 
        // <param name="boundingBox">The <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" /> to use.</param> 
        // <param name="customProperties">The set of custom properties associated with the <see cref="T:Azure.Core.GeoJson.GeoObject" />.</param> 
        public GeoPointCollection(IEnumerable<GeoPoint> points, GeoBoundingBox? boundingBox, IReadOnlyDictionary<string, object?> customProperties); 
        // <summary> 
        // Returns a view over the coordinates array that forms this geometry. 
        // </summary> 
        public GeoArray<GeoPosition> Coordinates { get; }
        // <summary>Gets the number of elements in the collection.</summary><returns>The number of elements in the collection. </returns> 
        public int Count { get; }
        // <summary>Gets the element at the specified index in the read-only list.</summary><param name="index">The zero-based index of the element to get. </param><returns>The element at the specified index in the read-only list.</returns> 
        public GeoPoint this[int index] { get; }
        // <summary> 
        // Gets the GeoJSON type of this object. 
        // </summary> 
        public override GeoObjectType Type { get; }
        // <summary>Returns an enumerator that iterates through the collection.</summary><returns>An enumerator that can be used to iterate through the collection.</returns> 
        public IEnumerator<GeoPoint> GetEnumerator(); 
        // <summary>Returns an enumerator that iterates through a collection.</summary><returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns> 
        IEnumerator IEnumerable.GetEnumerator(); 
    } 

    // <summary> 
    // Represents a polygon consisting of outer ring and optional inner rings. 
    // </summary> 
    // <example> 
    // Creating a polygon: 
    // <code snippet="Snippet:CreatePolygon" language="csharp"> 
    // var polygon = new GeoPolygon(new[] 
    // { 
    // new GeoPosition(-122.108727, 47.649383), 
    // new GeoPosition(-122.081538, 47.640846), 
    // new GeoPosition(-122.078634, 47.576066), 
    // new GeoPosition(-122.112686, 47.578559), 
    // new GeoPosition(-122.108727, 47.649383), 
    // }); 
    // </code> 
    // Creating a polygon with holes: 
    // <code snippet="Snippet:CreatePolygonWithHoles" language="csharp"> 
    // var polygon = new GeoPolygon(new[] 
    // { 
    // // Outer ring 
    // new GeoLinearRing(new[] 
    // { 
    // new GeoPosition(-122.108727, 47.649383), 
    // new GeoPosition(-122.081538, 47.640846), 
    // new GeoPosition(-122.078634, 47.576066), 
    // new GeoPosition(-122.112686, 47.578559), 
    // // Last position same as first 
    // new GeoPosition(-122.108727, 47.649383), 
    // }), 
    // // Inner ring 
    // new GeoLinearRing(new[] 
    // { 
    // new GeoPosition(-122.102370, 47.607370), 
    // new GeoPosition(-122.083488, 47.608007), 
    // new GeoPosition(-122.085419, 47.597879), 
    // new GeoPosition(-122.107005, 47.596895), 
    // // Last position same as first 
    // new GeoPosition(-122.102370, 47.607370), 
    // }) 
    // }); 
    // </code> 
    // </example> 
    [JsonConverter(typeof(GeoJsonConverter))] 
    public sealed class GeoPolygon : GeoObject { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPolygon" />. 
        // </summary> 
        // <param name="positions">The positions that make up the outer ring of the polygon.</param> 
        public GeoPolygon(IEnumerable<GeoPosition> positions); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPolygon" />. 
        // </summary> 
        // <param name="rings">The collection of rings that make up the polygon, first ring is the outer ring others are inner rings.</param> 
        public GeoPolygon(IEnumerable<GeoLinearRing> rings); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPolygon" />. 
        // </summary> 
        // <param name="rings">The collection of rings that make up the polygon, first ring is the outer ring others are inner rings.</param> 
        // <param name="boundingBox">The <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" /> to use.</param> 
        // <param name="customProperties">The set of custom properties associated with the <see cref="T:Azure.Core.GeoJson.GeoObject" />.</param> 
        public GeoPolygon(IEnumerable<GeoLinearRing> rings, GeoBoundingBox? boundingBox, IReadOnlyDictionary<string, object?> customProperties); 
        // <summary> 
        // Returns a view over the coordinates array that forms this geometry. 
        // </summary> 
        public GeoArray<GeoArray<GeoPosition>> Coordinates { get; }
        // <summary> 
        // Returns the outer ring of the polygon. 
        // </summary> 
        public GeoLinearRing OuterRing { get; }
        // <summary> 
        // Gets a set of rings that form the polygon. 
        // </summary> 
        public IReadOnlyList<GeoLinearRing> Rings { get; }
        // <summary> 
        // Gets the GeoJSON type of this object. 
        // </summary> 
        public override GeoObjectType Type { get; }
    } 

    // <summary> 
    // Represents a geometry that is composed of multiple <see cref="T:Azure.Core.GeoJson.GeoPolygon" />. 
    // </summary> 
    [JsonConverter(typeof(GeoJsonConverter))] 
    public sealed class GeoPolygonCollection : GeoObject, IReadOnlyList<GeoPolygon>, IReadOnlyCollection<GeoPolygon>, IEnumerable<GeoPolygon>, IEnumerable { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPolygonCollection" />. 
        // </summary> 
        // <param name="polygons">The collection of inner polygons.</param> 
        public GeoPolygonCollection(IEnumerable<GeoPolygon> polygons); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.GeoJson.GeoPolygonCollection" />. 
        // </summary> 
        // <param name="polygons">The collection of inner geometries.</param> 
        // <param name="boundingBox">The <see cref="T:Azure.Core.GeoJson.GeoBoundingBox" /> to use.</param> 
        // <param name="customProperties">The set of custom properties associated with the <see cref="T:Azure.Core.GeoJson.GeoObject" />.</param> 
        public GeoPolygonCollection(IEnumerable<GeoPolygon> polygons, GeoBoundingBox? boundingBox, IReadOnlyDictionary<string, object?> customProperties); 
        // <summary> 
        // Returns a view over the coordinates array that forms this geometry. 
        // </summary> 
        public GeoArray<GeoArray<GeoArray<GeoPosition>>> Coordinates { get; }
        // <summary>Gets the number of elements in the collection.</summary><returns>The number of elements in the collection. </returns> 
        public int Count { get; }
        // <summary>Gets the element at the specified index in the read-only list.</summary><param name="index">The zero-based index of the element to get. </param><returns>The element at the specified index in the read-only list.</returns> 
        public GeoPolygon this[int index] { get; }
        // <summary> 
        // Gets the GeoJSON type of this object. 
        // </summary> 
        public override GeoObjectType Type { get; }
        // <summary>Returns an enumerator that iterates through the collection.</summary><returns>An enumerator that can be used to iterate through the collection.</returns> 
        public IEnumerator<GeoPolygon> GetEnumerator(); 
        // <summary>Returns an enumerator that iterates through a collection.</summary><returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns> 
        IEnumerator IEnumerable.GetEnumerator(); 
    } 

    // <summary> 
    // Represents a single spatial position with latitude, longitude, and optional altitude. 
    // </summary> 
    public readonly struct GeoPosition : IEquatable<GeoPosition> { 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.Core.GeoJson.GeoPosition" />. 
        // </summary> 
        // <param name="longitude">The longitude of the position.</param> 
        // <param name="latitude">The latitude of the position.</param> 
        public GeoPosition(double longitude, double latitude); 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.Core.GeoJson.GeoPosition" />. 
        // </summary> 
        // <param name="longitude">The longitude of the position.</param> 
        // <param name="latitude">The latitude of the position.</param> 
        // <param name="altitude">The altitude of the position.</param> 
        public GeoPosition(double longitude, double latitude, double? altitude); 
        // <summary> 
        // Gets the altitude of the position. 
        // </summary> 
        public double? Altitude { get; }
        // <summary> 
        // Returns the count of the coordinate components. 
        // </summary> 
        public int Count { get; }
        // <summary> 
        // Gets the latitude of the position. 
        // </summary> 
        public double Latitude { get; }
        // <summary> 
        // Gets the longitude of the position. 
        // </summary> 
        public double Longitude { get; }
        // <summary> 
        // Get the value of coordinate component using its index. 
        // </summary> 
        // <param name="index"></param> 
        public double this[int index] { get; }
        // <summary> 
        // Determines whether two specified positions have the same value. 
        // </summary> 
        // <param name="left">The first position to compare.</param> 
        // <param name="right">The first position to compare.</param> 
        // <returns><c>true</c> if the value of <c>left</c> is the same as the value of <c>b</c>; otherwise, <c>false</c>.</returns> 
        public static bool operator ==(GeoPosition left, GeoPosition right); 
        // <summary> 
        // Determines whether two specified positions have the same value. 
        // </summary> 
        // <param name="left">The first position to compare.</param> 
        // <param name="right">The first position to compare.</param> 
        // <returns><c>false</c> if the value of <c>left</c> is the same as the value of <c>b</c>; otherwise, <c>true</c>.</returns> 
        public static bool operator !=(GeoPosition left, GeoPosition right); 
        // <summary>Indicates whether the current object is equal to another object of the same type.</summary><param name="other">An object to compare with this object.</param><returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns> 
        public bool Equals(GeoPosition other); 
        // <summary>Indicates whether this instance and a specified object are equal.</summary><param name="obj">The object to compare with the current instance. </param><returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns> 
        public override bool Equals(object? obj); 
        // <summary>Returns the hash code for this instance.</summary><returns>A 32-bit signed integer that is the hash code for this instance.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns the fully qualified type name of this instance.</summary><returns>The fully qualified type name.</returns> 
        public override string ToString(); 
    } 

} 

namespace Azure.Core.Pipeline { 
    // <summary> 
    // A policy that sends an <see cref="T:Azure.Core.AccessToken" /> provided by a <see cref="T:Azure.Core.TokenCredential" /> as an <see cref="P:Azure.Core.HttpHeader.Names.Authorization" /> header. 
    // </summary> 
    public class BearerTokenAuthenticationPolicy : HttpPipelinePolicy { 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy" /> using provided token credential and scope to authenticate for. 
        // </summary> 
        // <param name="credential">The token credential to use for authentication.</param> 
        // <param name="scope">The scope to be included in acquired tokens.</param> 
        public BearerTokenAuthenticationPolicy(TokenCredential credential, string scope); 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy" /> using provided token credential and scopes to authenticate for. 
        // </summary> 
        // <param name="credential">The token credential to use for authentication.</param> 
        // <param name="scopes">Scopes to be included in acquired tokens.</param> 
        // <exception cref="T:System.ArgumentNullException">When <paramref name="credential" /> or <paramref name="scopes" /> is null.</exception> 
        public BearerTokenAuthenticationPolicy(TokenCredential credential, IEnumerable<string> scopes); 
        // <summary> 
        // Applies the policy to the <paramref name="message" />. Implementers are expected to mutate <see cref="P:Azure.Core.HttpMessage.Request" /> before calling <see cref="M:Azure.Core.Pipeline.HttpPipelinePolicy.ProcessNextAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> and observe the <see cref="P:Azure.Core.HttpMessage.Response" /> changes after. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // Applies the policy to the <paramref name="message" />. Implementers are expected to mutate <see cref="P:Azure.Core.HttpMessage.Request" /> before calling <see cref="M:Azure.Core.Pipeline.HttpPipelinePolicy.ProcessNextAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> and observe the <see cref="P:Azure.Core.HttpMessage.Response" /> changes after. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask" /> representing the asynchronous operation.</returns> 
        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // Sets the Authorization header on the <see cref="T:Azure.Core.Request" /> by calling GetToken, or from cache, if possible. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> with the <see cref="T:Azure.Core.Request" /> to be authorized.</param> 
        // <param name="context">The <see cref="T:Azure.Core.TokenRequestContext" /> used to authorize the <see cref="T:Azure.Core.Request" />.</param> 
        protected void AuthenticateAndAuthorizeRequest(HttpMessage message, TokenRequestContext context); 
        // <summary> 
        // Sets the Authorization header on the <see cref="T:Azure.Core.Request" /> by calling GetToken, or from cache, if possible. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> with the <see cref="T:Azure.Core.Request" /> to be authorized.</param> 
        // <param name="context">The <see cref="T:Azure.Core.TokenRequestContext" /> used to authorize the <see cref="T:Azure.Core.Request" />.</param> 
        protected ValueTask AuthenticateAndAuthorizeRequestAsync(HttpMessage message, TokenRequestContext context); 
        // <summary> 
        // Executes before <see cref="M:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy.ProcessAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> or 
        // <see cref="M:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy.Process(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> is called. 
        // Implementers of this method are expected to call <see cref="M:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy.AuthenticateAndAuthorizeRequest(Azure.Core.HttpMessage,Azure.Core.TokenRequestContext)" /> or <see cref="M:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy.AuthenticateAndAuthorizeRequestAsync(Azure.Core.HttpMessage,Azure.Core.TokenRequestContext)" /> 
        // if authorization is required for requests not related to handling a challenge response. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        protected virtual void AuthorizeRequest(HttpMessage message); 
        // <summary> 
        // Executes before <see cref="M:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy.ProcessAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> or 
        // <see cref="M:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy.Process(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> is called. 
        // Implementers of this method are expected to call <see cref="M:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy.AuthenticateAndAuthorizeRequest(Azure.Core.HttpMessage,Azure.Core.TokenRequestContext)" /> or <see cref="M:Azure.Core.Pipeline.BearerTokenAuthenticationPolicy.AuthenticateAndAuthorizeRequestAsync(Azure.Core.HttpMessage,Azure.Core.TokenRequestContext)" /> 
        // if authorization is required for requests not related to handling a challenge response. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask" /> representing the asynchronous operation.</returns> 
        protected virtual ValueTask AuthorizeRequestAsync(HttpMessage message); 
        // <summary> 
        // Executed in the event a 401 response with a WWW-Authenticate authentication challenge header is received after the initial request. 
        // The default implementation will attempt to handle Continuous Access Evaluation (CAE) claims challenges. 
        // </summary> 
        // <remarks>Service client libraries may override this to handle service specific authentication challenges.</remarks> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> to be authenticated.</param> 
        // <returns>A boolean indicating whether the request was successfully authenticated and should be sent to the transport.</returns> 
        protected virtual bool AuthorizeRequestOnChallenge(HttpMessage message); 
        // <summary> 
        // Executed in the event a 401 response with a WWW-Authenticate authentication challenge header is received after the initial request. 
        // The default implementation will attempt to handle Continuous Access Evaluation (CAE) claims challenges. 
        // </summary> 
        // <remarks>Service client libraries may override this to handle service specific authentication challenges.</remarks> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> to be authenticated.</param> 
        // <returns>A boolean indicating whether the request was successfully authenticated and should be sent to the transport.</returns> 
        protected virtual ValueTask<bool> AuthorizeRequestOnChallengeAsync(HttpMessage message); 
    } 

    // <summary> 
    // An implementation of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> that may contain resources that require disposal. 
    // </summary> 
    public sealed class DisposableHttpPipeline : HttpPipeline, IDisposable { 
        // <summary> 
        // Disposes the underlying transport if it is owned by the client, i.e. it was created via the Build method on <see cref="T:Azure.Core.Pipeline.HttpPipelineBuilder" />. If the underlying transport is not owned by the client, i.e. it was supplied as a custom transport on <see cref="T:Azure.Core.ClientOptions" />, it will not be disposed. 
        // <remarks> 
        // The reason not to dispose a transport owned outside the client, i.e. one that was provided via <see cref="T:Azure.Core.ClientOptions" /> is to account for scenarios 
        // where the custom transport may be shared across clients. In this case, it is possible to dispose of a transport 
        // still in use by other clients. When the transport is created internally, it can properly determine if a shared instance is in use. 
        // </remarks> 
        // </summary> 
        public void Dispose(); 
    } 

    // <summary> 
    // An <see cref="T:Azure.Core.Pipeline.HttpPipelineTransport" /> implementation that uses <see cref="T:System.Net.Http.HttpClient" /> as the transport. 
    // </summary> 
    // <summary> 
    // An <see cref="T:Azure.Core.Pipeline.HttpPipelineTransport" /> implementation that uses <see cref="T:System.Net.Http.HttpClient" /> as the transport. 
    // </summary> 
    // <summary> 
    // An <see cref="T:Azure.Core.Pipeline.HttpPipelineTransport" /> implementation that uses <see cref="T:System.Net.Http.HttpClient" /> as the transport. 
    // </summary> 
    public class HttpClientTransport : HttpPipelineTransport, IDisposable { 
        // <summary> 
        // Creates a new <see cref="T:Azure.Core.Pipeline.HttpClientTransport" /> instance using default configuration. 
        // </summary> 
        public HttpClientTransport(); 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.Pipeline.HttpClientTransport" /> using the provided client instance. 
        // </summary> 
        // <param name="messageHandler">The instance of <see cref="T:System.Net.Http.HttpMessageHandler" /> to use.</param> 
        public HttpClientTransport(HttpMessageHandler messageHandler); 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.Pipeline.HttpClientTransport" /> using the provided client instance. 
        // </summary> 
        // <param name="client">The instance of <see cref="T:System.Net.Http.HttpClient" /> to use.</param> 
        public HttpClientTransport(HttpClient client); 
        // <summary> 
        // A shared instance of <see cref="T:Azure.Core.Pipeline.HttpClientTransport" /> with default parameters. 
        // </summary> 
        public static readonly HttpClientTransport Shared; 
        // <summary> 
        // Creates a new transport specific instance of <see cref="T:Azure.Core.Request" />. This should not be called directly, <see cref="M:Azure.Core.Pipeline.HttpPipeline.CreateRequest" /> or 
        // <see cref="M:Azure.Core.Pipeline.HttpPipeline.CreateMessage" /> should be used instead. 
        // </summary> 
        // <returns></returns> 
        public override sealed Request CreateRequest(); 
        // <summary> 
        // Disposes the underlying <see cref="T:System.Net.Http.HttpClient" />. 
        // </summary> 
        public void Dispose(); 
        // <summary> 
        // Sends the request contained by the <paramref name="message" /> and sets the <see cref="P:Azure.Core.HttpMessage.Response" /> property to received response synchronously. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> containing request and response.</param> 
        public override void Process(HttpMessage message); 
        // <summary> 
        // Sends the request contained by the <paramref name="message" /> and sets the <see cref="P:Azure.Core.HttpMessage.Response" /> property to received response asynchronously. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> containing request and response.</param> 
        public override ValueTask ProcessAsync(HttpMessage message); 
    } 

    // <summary> 
    // Represents a primitive for sending HTTP requests and receiving responses extensible by adding <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> processing steps. 
    // </summary> 
    public class HttpPipeline { 
        // <summary> 
        // Creates a new instance of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> with the provided transport, policies and response classifier. 
        // </summary> 
        // <param name="transport">The <see cref="T:Azure.Core.Pipeline.HttpPipelineTransport" /> to use for sending the requests.</param> 
        // <param name="policies">Policies to be invoked as part of the pipeline in order.</param> 
        // <param name="responseClassifier">The response classifier to be used in invocations.</param> 
        public HttpPipeline(HttpPipelineTransport transport, HttpPipelinePolicy[]? policies = null, ResponseClassifier? responseClassifier = null); 
        // <summary> 
        // The <see cref="P:Azure.Core.Pipeline.HttpPipeline.ResponseClassifier" /> instance used in this pipeline invocations. 
        // </summary> 
        public ResponseClassifier ResponseClassifier { get; }
        // <summary> 
        // Creates a scope in which all outgoing requests would use the provided 
        // </summary> 
        // <param name="clientRequestId">The client request id value to be sent with request.</param> 
        // <returns>The <see cref="T:System.IDisposable" /> instance that needs to be disposed when client request id shouldn't be sent anymore.</returns> 
        // <example> 
        // Sample usage: 
        // <code snippet="Snippet:ClientRequestId" language="csharp"> 
        // var secretClient = new SecretClient(new Uri("http://example.com"), new DefaultAzureCredential()); 
        //  
        // using (HttpPipeline.CreateClientRequestIdScope("&lt;custom-client-request-id&gt;")) 
        // { 
        // // The HTTP request resulting from the client call would have x-ms-client-request-id value set to &lt;custom-client-request-id&gt; 
        // secretClient.GetSecret("&lt;secret-name&gt;"); 
        // } 
        // </code> 
        // </example> 
        public static IDisposable CreateClientRequestIdScope(string? clientRequestId); 
        // <summary> 
        // Creates a scope in which all <see cref="T:Azure.Core.HttpMessage" />s would have provided properties. 
        // </summary> 
        // <param name="messageProperties">Properties to be added to <see cref="T:Azure.Core.HttpMessage" />s</param> 
        // <returns>The <see cref="T:System.IDisposable" /> instance that needs to be disposed when properties shouldn't be used anymore.</returns> 
        public static IDisposable CreateHttpMessagePropertiesScope(IDictionary<string, object?> messageProperties); 
        // <summary> 
        // Creates a new <see cref="T:Azure.Core.HttpMessage" /> instance. 
        // </summary> 
        // <returns>The message.</returns> 
        public HttpMessage CreateMessage(); 
        // <summary> 
        // </summary> 
        // <param name="context"></param> 
        // <returns></returns> 
        public HttpMessage CreateMessage(RequestContext? context); 
        // <summary> 
        // Creates a new <see cref="T:Azure.Core.HttpMessage" /> instance. 
        // </summary> 
        // <param name="context">Context specifying the message options.</param> 
        // <param name="classifier"></param> 
        // <returns>The message.</returns> 
        public HttpMessage CreateMessage(RequestContext? context, ResponseClassifier? classifier = null); 
        // <summary> 
        // Creates a new <see cref="T:Azure.Core.Request" /> instance. 
        // </summary> 
        // <returns>The request.</returns> 
        public Request CreateRequest(); 
        // <summary> 
        // Invokes the pipeline synchronously. After the task completes response would be set to the <see cref="P:Azure.Core.HttpMessage.Response" /> property. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> to send.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use.</param> 
        public void Send(HttpMessage message, CancellationToken cancellationToken); 
        // <summary> 
        // Invokes the pipeline asynchronously. After the task completes response would be set to the <see cref="P:Azure.Core.HttpMessage.Response" /> property. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> to send.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask" /> representing the asynchronous operation.</returns> 
        public ValueTask SendAsync(HttpMessage message, CancellationToken cancellationToken); 
        // <summary> 
        // Invokes the pipeline synchronously with the provided request. 
        // </summary> 
        // <param name="request">The <see cref="T:Azure.Core.Request" /> to send.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use.</param> 
        // <returns>The <see cref="T:Azure.Response" /> from the server.</returns> 
        public Response SendRequest(Request request, CancellationToken cancellationToken); 
        // <summary> 
        // Invokes the pipeline asynchronously with the provided request. 
        // </summary> 
        // <param name="request">The <see cref="T:Azure.Core.Request" /> to send.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask`1" /> representing the asynchronous operation.</returns> 
        public ValueTask<Response> SendRequestAsync(Request request, CancellationToken cancellationToken); 
    } 

    // <summary> 
    // Factory for creating instances of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> populated with default policies. 
    // </summary> 
    public static class HttpPipelineBuilder { 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> populated with default policies, user-provided policies from <paramref name="options" /> and client provided per call policies. 
        // </summary> 
        // <param name="options">The user-provided client options object.</param> 
        // <param name="perRetryPolicies">Client provided per-retry policies.</param> 
        // <returns>A new instance of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /></returns> 
        public static HttpPipeline Build(ClientOptions options, params HttpPipelinePolicy[] perRetryPolicies); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> populated with default policies, user-provided policies from <paramref name="options" /> and client provided per call policies. 
        // </summary> 
        // <param name="options">The user-provided client options object.</param> 
        // <param name="perCallPolicies">Client provided per-call policies.</param> 
        // <param name="perRetryPolicies">Client provided per-retry policies.</param> 
        // <param name="responseClassifier">The client provided response classifier.</param> 
        // <returns>A new instance of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /></returns> 
        public static HttpPipeline Build(ClientOptions options, HttpPipelinePolicy[] perCallPolicies, HttpPipelinePolicy[] perRetryPolicies, ResponseClassifier? responseClassifier); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.Pipeline.DisposableHttpPipeline" /> populated with default policies, user-provided policies from <paramref name="options" />, client provided per call policies, and the supplied <see cref="T:Azure.Core.Pipeline.HttpPipelineTransportOptions" />. 
        // </summary> 
        // <param name="options">The user-provided client options object.</param> 
        // <param name="perCallPolicies">Client provided per-call policies.</param> 
        // <param name="perRetryPolicies">Client provided per-retry policies.</param> 
        // <param name="transportOptions">The user-provided transport options which will be applied to the default transport. Note: If a custom transport has been supplied via the <paramref name="options" />, these <paramref name="transportOptions" /> will be ignored.</param> 
        // <param name="responseClassifier">The client provided response classifier.</param> 
        // <returns>A new instance of <see cref="T:Azure.Core.Pipeline.DisposableHttpPipeline" /></returns> 
        public static DisposableHttpPipeline Build(ClientOptions options, HttpPipelinePolicy[] perCallPolicies, HttpPipelinePolicy[] perRetryPolicies, HttpPipelineTransportOptions transportOptions, ResponseClassifier? responseClassifier); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> populated with default policies, user-provided policies from <paramref name="options" /> and client provided per call policies. 
        // </summary> 
        // <param name="options">The configuration options used to build the <see cref="T:Azure.Core.Pipeline.HttpPipeline" /></param> 
        // <returns>A new instance of <see cref="T:Azure.Core.Pipeline.HttpPipeline" /></returns> 
        public static HttpPipeline Build(HttpPipelineOptions options); 
        // <summary> 
        // Creates an instance of <see cref="T:Azure.Core.Pipeline.DisposableHttpPipeline" /> populated with default policies, user-provided policies from <paramref name="options" />, client provided per call policies, and the supplied <see cref="T:Azure.Core.Pipeline.HttpPipelineTransportOptions" />. 
        // </summary> 
        // <param name="options">The configuration options used to build the <see cref="T:Azure.Core.Pipeline.DisposableHttpPipeline" /></param> 
        // <param name="transportOptions">The user-provided transport options which will be applied to the default transport. Note: If a custom transport has been supplied via the <paramref name="options" />, these <paramref name="transportOptions" /> will be ignored.</param> 
        // <returns>A new instance of <see cref="T:Azure.Core.Pipeline.DisposableHttpPipeline" /></returns> 
        public static DisposableHttpPipeline Build(HttpPipelineOptions options, HttpPipelineTransportOptions transportOptions); 
    } 

    // <summary> 
    // Specifies configuration of options for building the <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> 
    // </summary> 
    public class HttpPipelineOptions { 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.Core.Pipeline.HttpPipelineOptions" />. 
        // </summary> 
        // <param name="options">The customer provided client options object.</param> 
        public HttpPipelineOptions(ClientOptions options); 
        // <summary> 
        // The customer provided client options object. 
        // </summary> 
        public ClientOptions ClientOptions { get; }
        // <summary> 
        // Client provided per-call policies. 
        // </summary> 
        public IList<HttpPipelinePolicy> PerCallPolicies { get; }
        // <summary> 
        // Client provided per-retry policies. 
        // </summary> 
        public IList<HttpPipelinePolicy> PerRetryPolicies { get; }
        // <summary> 
        // Responsible for parsing the error content related to a failed request from the service. 
        // </summary> 
        public RequestFailedDetailsParser RequestFailedDetailsParser { get; set; }
        // <summary> 
        // The client provided response classifier. 
        // </summary> 
        public ResponseClassifier? ResponseClassifier { get; set; }
    } 

    // <summary> 
    // Represent an extension point for the <see cref="T:Azure.Core.Pipeline.HttpPipeline" /> that can mutate the <see cref="T:Azure.Core.Request" /> and react to received <see cref="T:Azure.Response" />. 
    // </summary> 
    public abstract class HttpPipelinePolicy { 
        protected HttpPipelinePolicy(); 
        // <summary> 
        // Invokes the next <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> in the <paramref name="pipeline" />. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> next policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after next one.</param> 
        protected static void ProcessNext(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // Invokes the next <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> in the <paramref name="pipeline" />. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> next policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after next one.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask" /> representing the asynchronous operation.</returns> 
        protected static ValueTask ProcessNextAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // Applies the policy to the <paramref name="message" />. Implementers are expected to mutate <see cref="P:Azure.Core.HttpMessage.Request" /> before calling <see cref="M:Azure.Core.Pipeline.HttpPipelinePolicy.ProcessNextAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> and observe the <see cref="P:Azure.Core.HttpMessage.Response" /> changes after. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        public abstract void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // Applies the policy to the <paramref name="message" />. Implementers are expected to mutate <see cref="P:Azure.Core.HttpMessage.Request" /> before calling <see cref="M:Azure.Core.Pipeline.HttpPipelinePolicy.ProcessNextAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> and observe the <see cref="P:Azure.Core.HttpMessage.Response" /> changes after. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask" /> representing the asynchronous operation.</returns> 
        public abstract ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
    } 

    // <summary> 
    // Represents a <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> that doesn't do any asynchronous or synchronously blocking operations. 
    // </summary> 
    internal [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] 
    public abstract class HttpPipelineSynchronousPolicy : HttpPipelinePolicy { 
        // <summary> 
        // Initializes a new instance of <see cref="T:Azure.Core.Pipeline.HttpPipelineSynchronousPolicy" /> 
        // </summary> 
        protected HttpPipelineSynchronousPolicy(); 
        // <summary> 
        // Method is invoked after the response is received. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> containing the response.</param> 
        public virtual void OnReceivedResponse(HttpMessage message); 
        // <summary> 
        // Method is invoked before the request is sent. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> containing the request.</param> 
        public virtual void OnSendingRequest(HttpMessage message); 
        // <summary> 
        // Applies the policy to the <paramref name="message" />. Implementers are expected to mutate <see cref="P:Azure.Core.HttpMessage.Request" /> before calling <see cref="M:Azure.Core.Pipeline.HttpPipelinePolicy.ProcessNextAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> and observe the <see cref="P:Azure.Core.HttpMessage.Response" /> changes after. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // Applies the policy to the <paramref name="message" />. Implementers are expected to mutate <see cref="P:Azure.Core.HttpMessage.Request" /> before calling <see cref="M:Azure.Core.Pipeline.HttpPipelinePolicy.ProcessNextAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> and observe the <see cref="P:Azure.Core.HttpMessage.Response" /> changes after. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask" /> representing the asynchronous operation.</returns> 
        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
    } 

    // <summary> 
    // Represents an HTTP pipeline transport used to send HTTP requests and receive responses. 
    // </summary> 
    public abstract class HttpPipelineTransport { 
        protected HttpPipelineTransport(); 
        // <summary> 
        // Creates a new transport specific instance of <see cref="T:Azure.Core.Request" />. This should not be called directly, <see cref="M:Azure.Core.Pipeline.HttpPipeline.CreateRequest" /> or 
        // <see cref="M:Azure.Core.Pipeline.HttpPipeline.CreateMessage" /> should be used instead. 
        // </summary> 
        // <returns></returns> 
        public abstract Request CreateRequest(); 
        // <summary> 
        // Sends the request contained by the <paramref name="message" /> and sets the <see cref="P:Azure.Core.HttpMessage.Response" /> property to received response synchronously. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> containing request and response.</param> 
        public abstract void Process(HttpMessage message); 
        // <summary> 
        // Sends the request contained by the <paramref name="message" /> and sets the <see cref="P:Azure.Core.HttpMessage.Response" /> property to received response asynchronously. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> containing request and response.</param> 
        public abstract ValueTask ProcessAsync(HttpMessage message); 
    } 

    // <summary> 
    // Enables configuration of options for the <see cref="T:Azure.Core.Pipeline.HttpClientTransport" /> 
    // </summary> 
    public class HttpPipelineTransportOptions { 
        // <summary> 
        // Initializes an instance of <see cref="T:Azure.Core.Pipeline.HttpPipelineTransportOptions" />. 
        // </summary> 
        public HttpPipelineTransportOptions(); 
        // <summary> 
        // The client certificate collection that will be configured for the transport. 
        // </summary> 
        // <value></value> 
        public IList<X509Certificate2> ClientCertificates { get; }
        // <summary> 
        // Gets or sets a value that indicates whether the redirect policy should follow redirection responses. 
        // </summary> 
        // <value> 
        // <c>true</c> if the redirect policy should follow redirection responses; otherwise <c>false</c>. The default value is <c>false</c>. 
        // </value> 
        public bool IsClientRedirectEnabled { get; set; }
        // <summary> 
        // A delegate that validates the certificate presented by the server. 
        // </summary> 
        public Func<ServerCertificateCustomValidationArgs, bool>? ServerCertificateCustomValidationCallback { get; set; }
    } 

    // <summary> 
    // A pipeline policy that detects a redirect response code and resends the request to the 
    // location specified by the response. 
    // </summary> 
    public sealed class RedirectPolicy : HttpPipelinePolicy { 
        // <summary> 
        // Sets a value that indicates whether redirects will be automatically followed for this message. 
        // </summary> 
        // <param name="message"></param> 
        // <param name="allowAutoRedirect"></param> 
        public static void SetAllowAutoRedirect(HttpMessage message, bool allowAutoRedirect); 
        // <summary> 
        // Applies the policy to the <paramref name="message" />. Implementers are expected to mutate <see cref="P:Azure.Core.HttpMessage.Request" /> before calling <see cref="M:Azure.Core.Pipeline.HttpPipelinePolicy.ProcessNextAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> and observe the <see cref="P:Azure.Core.HttpMessage.Response" /> changes after. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // Applies the policy to the <paramref name="message" />. Implementers are expected to mutate <see cref="P:Azure.Core.HttpMessage.Request" /> before calling <see cref="M:Azure.Core.Pipeline.HttpPipelinePolicy.ProcessNextAsync(Azure.Core.HttpMessage,System.ReadOnlyMemory{Azure.Core.Pipeline.HttpPipelinePolicy})" /> and observe the <see cref="P:Azure.Core.HttpMessage.Response" /> changes after. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask" /> representing the asynchronous operation.</returns> 
        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
    } 

    // <summary> 
    // Represents a policy that can be overriden to customize whether or not a request will be retried and how long to wait before retrying. 
    // </summary> 
    public class RetryPolicy : HttpPipelinePolicy { 
        // <summary> 
        // Initializes a new instance of the <see cref="T:Azure.Core.Pipeline.RetryPolicy" /> class. 
        // </summary> 
        // <param name="maxRetries">The maximum number of retries to attempt.</param> 
        // <param name="delayStrategy">The delay to use for computing the interval between retry attempts.</param> 
        public RetryPolicy(int maxRetries = 3, DelayStrategy? delayStrategy = null); 
        // <summary> 
        // This method can be overriden to take full control over the retry policy. If this is overriden and the base method isn't called, 
        // it is the implementer's responsibility to populate the <see cref="P:Azure.Core.HttpMessage.ProcessingContext" /> property. 
        // This method will only be called for sync methods. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // This method can be overriden to take full control over the retry policy. If this is overriden and the base method isn't called, 
        // it is the implementer's responsibility to populate the <see cref="P:Azure.Core.HttpMessage.ProcessingContext" /> property. 
        // This method will only be called for async methods. 
        // </summary> 
        // <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> this policy would be applied to.</param> 
        // <param name="pipeline">The set of <see cref="T:Azure.Core.Pipeline.HttpPipelinePolicy" /> to execute after current one.</param> 
        // <returns>The <see cref="T:System.Threading.Tasks.ValueTask" /> representing the asynchronous operation.</returns> 
        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline); 
        // <summary> 
        // This method can be overridden to introduce logic that runs after the request is sent through the pipeline and control is returned to the retry 
        // policy. This method will only be called for sync methods. 
        // </summary> 
        // <param name="message">The message containing the request and response.</param> 
        protected virtual void OnRequestSent(HttpMessage message); 
        // <summary> 
        // This method can be overridden to introduce logic that runs after the request is sent through the pipeline and control is returned to the retry 
        // policy. This method will only be called for async methods. 
        // </summary> 
        // <param name="message">The message containing the request and response.</param> 
        protected virtual ValueTask OnRequestSentAsync(HttpMessage message); 
        // <summary> 
        // This method can be overridden to introduce logic before each request attempt is sent. This will run even for the first attempt. 
        // This method will only be called for sync methods. 
        // </summary> 
        // <param name="message">The message containing the request and response.</param> 
        protected virtual void OnSendingRequest(HttpMessage message); 
        // <summary> 
        // This method can be overriden to introduce logic that runs before the request is sent. This will run even for the first attempt. 
        // This method will only be called for async methods. 
        // </summary> 
        // <param name="message">The message containing the request and response.</param> 
        protected virtual ValueTask OnSendingRequestAsync(HttpMessage message); 
        // <summary> 
        // This method can be overriden to control whether a request should be retried. It will be called for any response where 
        // <see cref="P:Azure.Response.IsError" /> is true, or if an exception is thrown from any subsequent pipeline policies or the transport. 
        // This method will only be called for sync methods. 
        // </summary> 
        // <param name="message">The message containing the request and response.</param> 
        // <param name="exception">The exception that occurred, if any, which can be used to determine if a retry should occur.</param> 
        // <returns>Whether or not to retry.</returns> 
        protected virtual bool ShouldRetry(HttpMessage message, Exception? exception); 
        // <summary> 
        // This method can be overriden to control whether a request should be retried.  It will be called for any response where 
        // <see cref="P:Azure.Response.IsError" /> is true, or if an exception is thrown from any subsequent pipeline policies or the transport. 
        // This method will only be called for async methods. 
        // </summary> 
        // <param name="message">The message containing the request and response.</param> 
        // <param name="exception">The exception that occurred, if any, which can be used to determine if a retry should occur.</param> 
        // <returns>Whether or not to retry.</returns> 
        protected virtual ValueTask<bool> ShouldRetryAsync(HttpMessage message, Exception? exception); 
    } 

    // <summary> 
    // Enables configuration of options for the <see cref="T:Azure.Core.Pipeline.HttpClientTransport" /> 
    // </summary> 
    public class ServerCertificateCustomValidationArgs { 
        // <summary> 
        // Initializes an instance of <see cref="T:Azure.Core.Pipeline.ServerCertificateCustomValidationArgs" />. 
        // </summary> 
        // <param name="certificate">The certificate</param> 
        // <param name="certificateAuthorityChain"></param> 
        // <param name="sslPolicyErrors"></param> 
        public ServerCertificateCustomValidationArgs(X509Certificate2? certificate, X509Chain? certificateAuthorityChain, SslPolicyErrors sslPolicyErrors); 
        // <summary> 
        // The certificate used to authenticate the remote party. 
        // </summary> 
        public X509Certificate2? Certificate { get; }
        // <summary> 
        // The chain of certificate authorities associated with the remote certificate. 
        // </summary> 
        public X509Chain? CertificateAuthorityChain { get; }
        // <summary> 
        // One or more errors associated with the remote certificate. 
        // </summary> 
        public SslPolicyErrors SslPolicyErrors { get; }
    } 

} 

namespace Azure.Core.Serialization { 
    // <summary> 
    // A dynamic abstraction over content data, such as JSON. 
    //  
    // This and related types are not intended to be mocked. 
    // </summary> 
    [DebuggerDisplay("{DebuggerDisplay,nq}")] 
    [JsonConverter(typeof(DynamicDataJsonConverter))] 
    public sealed class DynamicData : IDisposable, IDynamicMetaObjectProvider { 
        // <summary> 
        // Determines whether the specified <see cref="T:Azure.Core.Serialization.DynamicData" /> and <see cref="T:System.Object" /> have the same value. 
        // </summary> 
        // <remarks> 
        // This operator calls through to <see cref="M:Azure.Core.Serialization.DynamicData.Equals(System.Object)" /> when DynamicData is on the left-hand 
        // side of the operation.  <see cref="M:Azure.Core.Serialization.DynamicData.Equals(System.Object)" /> has value semantics when the DynamicData represents 
        // a JSON primitive, i.e. string, bool, number, or null, and reference semantics otherwise, i.e. for objects and arrays. 
        //  
        // Please note that if DynamicData is on the right-hand side of a <c>==</c> operation, this operator will not be invoked. 
        // Because of this the result of a <c>==</c> comparison with <c>null</c> on the left and a DynamicData instance on the right will return <c>false</c>. 
        // </remarks> 
        // <param name="left">The <see cref="T:Azure.Core.Serialization.DynamicData" /> to compare.</param> 
        // <param name="right">The <see cref="T:System.Object" /> to compare.</param> 
        // <returns><c>true</c> if the value of <paramref name="left" /> is the same as the value of <paramref name="right" />; otherwise, <c>false</c>.</returns> 
        public static bool operator ==(DynamicData? left, object? right); 
        // <summary> 
        // Converts the value to a <see cref="T:System.DateTime" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static explicit operator DateTime(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.DateTimeOffset" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static explicit operator DateTimeOffset(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Guid" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static explicit operator Guid(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Boolean" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator bool(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.String" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator string?(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Byte" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator byte(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.SByte" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator sbyte(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Int16" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator short(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.UInt16" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator ushort(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Int32" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator int(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.UInt32" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator uint(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Int64" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator long(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.UInt64" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator ulong(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Single" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator float(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Double" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator double(DynamicData value); 
        // <summary> 
        // Converts the value to a <see cref="T:System.Decimal" />. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        public static implicit operator decimal(DynamicData value); 
        // <summary> 
        // Determines whether the specified <see cref="T:Azure.Core.Serialization.DynamicData" /> and <see cref="T:System.Object" /> have different values. 
        // </summary> 
        // <remarks> 
        // This operator calls through to <see cref="M:Azure.Core.Serialization.DynamicData.Equals(System.Object)" /> when DynamicData is on the left-hand 
        // side of the operation.  <see cref="M:Azure.Core.Serialization.DynamicData.Equals(System.Object)" /> has value semantics when the DynamicData represents 
        // a JSON primitive, i.e. string, bool, number, or null, and reference semantics otherwise, i.e. for objects and arrays. 
        // </remarks> 
        // <param name="left">The <see cref="T:Azure.Core.Serialization.DynamicData" /> to compare.</param> 
        // <param name="right">The <see cref="T:System.Object" /> to compare.</param> 
        // <returns><c>true</c> if the value of <paramref name="left" /> is different from the value of <paramref name="right" />; otherwise, <c>false</c>.</returns> 
        public static bool operator !=(DynamicData? left, object? right); 
        // <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary> 
        public void Dispose(); 
        // <summary>Returns the <see cref="T:System.Dynamic.DynamicMetaObject" /> responsible for binding operations performed on this object.</summary><param name="parameter">The expression tree representation of the runtime value.</param><returns>The <see cref="T:System.Dynamic.DynamicMetaObject" /> to bind this object.</returns> 
        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter); 
        // <summary>Determines whether the specified object is equal to the current object.</summary><param name="obj">The object to compare with the current object. </param><returns>true if the specified object  is equal to the current object; otherwise, false.</returns> 
        public override bool Equals(object? obj); 
        // <summary>Serves as the default hash function. </summary><returns>A hash code for the current object.</returns> 
        public override int GetHashCode(); 
        // <summary>Returns a string that represents the current object.</summary><returns>A string that represents the current object.</returns> 
        public override string ToString(); 
    } 

    // <summary> 
    // Converts type member names to serializable member names. 
    // </summary> 
    public interface IMemberNameConverter { 
        // <summary> 
        // Converts a <see cref="T:System.Reflection.MemberInfo" /> to a serializable member name. 
        // </summary> 
        // <param name="member">The <see cref="T:System.Reflection.MemberInfo" /> to convert to a serializable member name.</param> 
        // <returns>The serializable member name, or null if the member is not defined or ignored by the serializer.</returns> 
        // <exception cref="T:System.ArgumentNullException"><paramref name="member" /> is null.</exception> 
        string? ConvertMemberName(MemberInfo member); 
    } 

    // <summary> 
    // An <see cref="T:Azure.Core.Serialization.ObjectSerializer" /> implementation that uses <see cref="T:System.Text.Json.JsonSerializer" /> for serialization/deserialization. 
    // </summary> 
    public class JsonObjectSerializer : ObjectSerializer, IMemberNameConverter { 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.Serialization.JsonObjectSerializer" />. 
        // </summary> 
        public JsonObjectSerializer(); 
        // <summary> 
        // Initializes new instance of <see cref="T:Azure.Core.Serialization.JsonObjectSerializer" />. 
        // </summary> 
        // <param name="options">The <see cref="T:System.Text.Json.JsonSerializerOptions" /> instance to use when serializing/deserializing.</param> 
        // <exception cref="T:System.ArgumentNullException"><paramref name="options" /> is null.</exception> 
        public JsonObjectSerializer(JsonSerializerOptions options); 
        // <summary> 
        // A shared instance of <see cref="T:Azure.Core.Serialization.JsonObjectSerializer" />, initialized with the default options. 
        // </summary> 
        public static JsonObjectSerializer Default { get; }
        // <summary> 
        // Read the binary representation into a <paramref name="returnType" />. 
        // The Stream will be read to completion. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to read from.</param> 
        // <param name="returnType">The type of the object to convert to and return.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during deserialization.</param> 
        public override object? Deserialize(Stream stream, Type returnType, CancellationToken cancellationToken); 
        // <summary> 
        // Read the binary representation into a <paramref name="returnType" />. 
        // The Stream will be read to completion. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to read from.</param> 
        // <param name="returnType">The type of the object to convert to and return.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during deserialization.</param> 
        public override ValueTask<object?> DeserializeAsync(Stream stream, Type returnType, CancellationToken cancellationToken); 
        // <summary> 
        // Convert the provided value to it's binary representation and write it to <see cref="T:System.IO.Stream" />. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to write to.</param> 
        // <param name="value">The value to convert.</param> 
        // <param name="inputType">The type of the <paramref name="value" /> to convert.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during serialization.</param> 
        public override void Serialize(Stream stream, object? value, Type inputType, CancellationToken cancellationToken); 
        // <summary> 
        // Convert the provided value to it's binary representation and return it as a <see cref="T:System.BinaryData" /> instance. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        // <param name="inputType">The type to use when serializing <paramref name="value" />. If omitted, the type will be determined using <see cref="M:System.Object.GetType" />().</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during serialization.</param> 
        // <returns>The object's binary representation as <see cref="T:System.BinaryData" />.</returns> 
        public override BinaryData Serialize(object? value, Type? inputType = null, CancellationToken cancellationToken = default); 
        // <summary> 
        // Convert the provided value to it's binary representation and write it to <see cref="T:System.IO.Stream" />. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to write to.</param> 
        // <param name="value">The value to convert.</param> 
        // <param name="inputType">The type of the <paramref name="value" /> to convert.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during serialization.</param> 
        public override ValueTask SerializeAsync(Stream stream, object? value, Type inputType, CancellationToken cancellationToken); 
        // <summary> 
        // Convert the provided value to it's binary representation and return it as a <see cref="T:System.BinaryData" /> instance. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        // <param name="inputType">The type to use when serializing <paramref name="value" />. If omitted, the type will be determined using <see cref="M:System.Object.GetType" />().</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during serialization.</param> 
        // <returns>The object's binary representation as <see cref="T:System.BinaryData" />.</returns> 
        public override ValueTask<BinaryData> SerializeAsync(object? value, Type? inputType = null, CancellationToken cancellationToken = default); 
        // <summary> 
        // Converts a <see cref="T:System.Reflection.MemberInfo" /> to a serializable member name. 
        // </summary> 
        // <param name="member">The <see cref="T:System.Reflection.MemberInfo" /> to convert to a serializable member name.</param> 
        // <returns>The serializable member name, or null if the member is not defined or ignored by the serializer.</returns> 
        // <exception cref="T:System.ArgumentNullException"><paramref name="member" /> is null.</exception> 
        string? IMemberNameConverter.ConvertMemberName(MemberInfo member); 
    } 

    // <summary> 
    // The format of property names in dynamic and serialized JSON content. 
    // </summary> 
    public enum JsonPropertyNames { 
        // <summary> 
        // Exact property name matches will be used with JSON property names. 
        // </summary> 
        UseExact = 0, 
        // <summary> 
        // Indicates that the JSON content uses a camel-case format for property names. 
        // </summary> 
        CamelCase = 1, 
    } 

    // <summary> 
    // An abstraction for reading typed objects. 
    // </summary> 
    public abstract class ObjectSerializer { 
        protected ObjectSerializer(); 
        // <summary> 
        // Read the binary representation into a <paramref name="returnType" />. 
        // The Stream will be read to completion. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to read from.</param> 
        // <param name="returnType">The type of the object to convert to and return.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during deserialization.</param> 
        public abstract object? Deserialize(Stream stream, Type returnType, CancellationToken cancellationToken); 
        // <summary> 
        // Read the binary representation into a <paramref name="returnType" />. 
        // The Stream will be read to completion. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to read from.</param> 
        // <param name="returnType">The type of the object to convert to and return.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during deserialization.</param> 
        public abstract ValueTask<object?> DeserializeAsync(Stream stream, Type returnType, CancellationToken cancellationToken); 
        // <summary> 
        // Convert the provided value to it's binary representation and write it to <see cref="T:System.IO.Stream" />. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to write to.</param> 
        // <param name="value">The value to convert.</param> 
        // <param name="inputType">The type of the <paramref name="value" /> to convert.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during serialization.</param> 
        public abstract void Serialize(Stream stream, object? value, Type inputType, CancellationToken cancellationToken); 
        // <summary> 
        // Convert the provided value to it's binary representation and return it as a <see cref="T:System.BinaryData" /> instance. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        // <param name="inputType">The type to use when serializing <paramref name="value" />. If omitted, the type will be determined using <see cref="M:System.Object.GetType" />().</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during serialization.</param> 
        // <returns>The object's binary representation as <see cref="T:System.BinaryData" />.</returns> 
        public virtual BinaryData Serialize(object? value, Type? inputType = null, CancellationToken cancellationToken = default); 
        // <summary> 
        // Convert the provided value to it's binary representation and write it to <see cref="T:System.IO.Stream" />. 
        // </summary> 
        // <param name="stream">The <see cref="T:System.IO.Stream" /> to write to.</param> 
        // <param name="value">The value to convert.</param> 
        // <param name="inputType">The type of the <paramref name="value" /> to convert.</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during serialization.</param> 
        public abstract ValueTask SerializeAsync(Stream stream, object? value, Type inputType, CancellationToken cancellationToken); 
        // <summary> 
        // Convert the provided value to it's binary representation and return it as a <see cref="T:System.BinaryData" /> instance. 
        // </summary> 
        // <param name="value">The value to convert.</param> 
        // <param name="inputType">The type to use when serializing <paramref name="value" />. If omitted, the type will be determined using <see cref="M:System.Object.GetType" />().</param> 
        // <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" /> to use during serialization.</param> 
        // <returns>The object's binary representation as <see cref="T:System.BinaryData" />.</returns> 
        public virtual ValueTask<BinaryData> SerializeAsync(object? value, Type? inputType = null, CancellationToken cancellationToken = default); 
    } 

} 

namespace Azure.Messaging { 
    // <summary> Represents a CloudEvent conforming to the 1.0 schema. This type has built-in serialization using System.Text.Json.</summary> 
    [JsonConverter(typeof(CloudEventConverter))] 
    public class CloudEvent { 
        // <summary> Initializes a new instance of the <see cref="T:Azure.Messaging.CloudEvent" /> class. </summary> 
        // <param name="source"> Identifies the context in which an event happened. The combination of id and source must be unique for each distinct event. </param> 
        // <param name="type"> Type of event related to the originating occurrence. For example, "Contoso.Items.ItemReceived". </param> 
        // <param name="jsonSerializableData"> Event data specific to the event type. </param> 
        // <param name="dataSerializationType">The type to use when serializing the data. 
        // If not specified, <see cref="M:System.Object.GetType" /> will be used on <paramref name="jsonSerializableData" />.</param> 
        // <exception cref="T:System.ArgumentNullException"> 
        // <paramref name="source" /> or <paramref name="type" /> was null. 
        // </exception> 
        public CloudEvent(string source, string type, object? jsonSerializableData, Type? dataSerializationType = null); 
        // <summary> Initializes a new instance of the <see cref="T:Azure.Messaging.CloudEvent" /> class using binary event data.</summary> 
        // <param name="source"> Identifies the context in which an event happened. The combination of id and source must be unique for each distinct event. </param> 
        // <param name="type"> Type of event related to the originating occurrence. For example, "Contoso.Items.ItemReceived". </param> 
        // <param name="data"> Binary event data specific to the event type. </param> 
        // <param name="dataContentType"> Content type of the payload. A content type different from "application/json" should be specified if payload is not JSON. </param> 
        // <param name="dataFormat">The format that the data of a <see cref="T:Azure.Messaging.CloudEvent" /> should be sent in 
        // when using the JSON envelope format.</param> 
        // <exception cref="T:System.ArgumentNullException"> 
        // <paramref name="source" /> or <paramref name="type" /> was null. 
        // </exception> 
        public CloudEvent(string source, string type, BinaryData? data, string? dataContentType, CloudEventDataFormat dataFormat = Binary); 
        // <summary> 
        // Gets or sets the event data as <see cref="T:System.BinaryData" />. Using BinaryData, 
        // one can deserialize the payload into rich data, or access the raw JSON data using <see cref="M:System.BinaryData.ToString" />. 
        // </summary> 
        public BinaryData? Data { get; set; }
        // <summary>Gets or sets the content type of the data.</summary> 
        public string? DataContentType { get; set; }
        // <summary>Gets or sets the schema that the data adheres to.</summary> 
        public string? DataSchema { get; set; }
        // <summary> 
        // Gets extension attributes that can be additionally added to the CloudEvent envelope. 
        // </summary> 
        public IDictionary<string, object> ExtensionAttributes { get; }
        // <summary> 
        // Gets or sets an identifier for the event. The combination of <see cref="P:Azure.Messaging.CloudEvent.Id" /> and <see cref="P:Azure.Messaging.CloudEvent.Source" /> must be unique for each distinct event. 
        // If not explicitly set, this will default to a <see cref="T:System.Guid" />. 
        // </summary> 
        public string Id { get; set; }
        // <summary>Gets or sets the context in which an event happened. The combination of <see cref="P:Azure.Messaging.CloudEvent.Id" /> 
        // and <see cref="P:Azure.Messaging.CloudEvent.Source" /> must be unique for each distinct event.</summary> 
        public string Source { get; set; }
        // <summary>Gets or sets the subject of the event in the context of the event producer (identified by source). </summary> 
        public string? Subject { get; set; }
        // <summary> 
        // Gets or sets the time (in UTC) the event was generated, in RFC3339 format. 
        // If not explicitly set, this will default to the time that the event is constructed. 
        // </summary> 
        public DateTimeOffset? Time { get; set; }
        // <summary>Gets or sets the type of event related to the originating occurrence.</summary> 
        public string Type { get; set; }
        // <summary> 
        // Given a single JSON-encoded event, parses the event envelope and returns a <see cref="T:Azure.Messaging.CloudEvent" />. 
        // If the specified event is not valid JSON an exception is thrown. 
        // By default, if the event is missing required properties, an exception is thrown though this can be relaxed 
        // by setting the <paramref name="skipValidation" /> parameter. 
        // </summary> 
        // <param name="json">An instance of <see cref="T:System.BinaryData" /> containing the JSON for the CloudEvent.</param> 
        // <param name="skipValidation">Set to <see langword="true" /> to allow missing or invalid properties to still parse into a CloudEvent. 
        // In particular, by setting strict to <see langword="true" />, the source, id, specversion and type properties are no longer required 
        // to be present in the JSON. Additionally, the casing requirements of the extension attribute names are relaxed. 
        // </param> 
        // <returns> A <see cref="T:Azure.Messaging.CloudEvent" />. </returns> 
        // <exception cref="T:System.ArgumentException"> 
        // <paramref name="json" /> contained multiple events. <see cref="M:Azure.Messaging.CloudEvent.ParseMany(System.BinaryData,System.Boolean)" /> should be used instead. 
        // </exception> 
        public static CloudEvent? Parse(BinaryData json, bool skipValidation = false); 
        // <summary> 
        // Given JSON-encoded events, parses the event envelope and returns an array of CloudEvents. 
        // If the specified event is not valid JSON an exception is thrown. 
        // By default, if the event is missing required properties, an exception is thrown though this can be relaxed 
        // by setting the <paramref name="skipValidation" /> parameter. 
        // </summary> 
        // <param name="json">An instance of <see cref="T:System.BinaryData" /> containing the JSON for one or more CloudEvents.</param> 
        // <param name="skipValidation">Set to <see langword="true" /> to allow missing or invalid properties to still parse into a CloudEvent. 
        // In particular, by setting strict to <see langword="true" />, the source, id, specversion and type properties are no longer required 
        // to be present in the JSON. Additionally, the casing requirements of the extension attribute names are relaxed. 
        // </param> 
        // <returns> An array of <see cref="T:Azure.Messaging.CloudEvent" /> instances.</returns> 
        public static CloudEvent[] ParseMany(BinaryData json, bool skipValidation = false); 
    } 

    // <summary> 
    // Specifies the format that the data of a <see cref="T:Azure.Messaging.CloudEvent" /> should be sent in 
    // when using the JSON envelope format for a <see cref="T:Azure.Messaging.CloudEvent" />. 
    // <see href="https://github.com/cloudevents/spec/blob/v1.0/json-format.md#31-handling-of-data" />. 
    // </summary> 
    public enum CloudEventDataFormat { 
        // <summary> 
        // Indicates the <see cref="P:Azure.Messaging.CloudEvent.Data" /> should be serialized as binary data. 
        // This data will be included as a Base64 encoded string in the "data_base64" 
        // field of the JSON payload. 
        // </summary> 
        Binary = 0, 
        // <summary> 
        // Indicates the <see cref="P:Azure.Messaging.CloudEvent.Data" /> should be serialized as JSON. 
        // The data will be included in the "data" field of the JSON payload. 
        // </summary> 
        Json = 1, 
    } 

    // <summary> 
    // The content of a message containing a content type along with the message data. 
    // </summary> 
    public class MessageContent { 
        public MessageContent(); 
        // <summary> 
        // Gets or sets the content type. 
        // </summary> 
        public virtual ContentType? ContentType { get; set; }
        // <summary> 
        // Gets or sets the data. 
        // </summary> 
        public virtual BinaryData? Data { get; set; }
        // <summary> 
        // Gets whether the message is read only or not. This 
        // can be overriden by inheriting classes to specify whether or 
        // not the message can be modified. 
        // </summary> 
        public virtual bool IsReadOnly { get; }
        // <summary> 
        // For inheriting types that have a string ContentType property, this property should be overriden to forward 
        // the <see cref="P:Azure.Messaging.MessageContent.ContentType" /> property into the inheriting type's string property, and vice versa. 
        // For types that have a <see cref="T:Azure.Core.ContentType" /> ContentType property, it is not necessary to override this member. 
        // </summary> 
        protected virtual ContentType? ContentTypeCore { get; set; }
    } 

} 

```