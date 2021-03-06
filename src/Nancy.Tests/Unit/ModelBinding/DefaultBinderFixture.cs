namespace Nancy.Tests.Unit.ModelBinding
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Globalization;
    using System.Xml.Serialization;

    using FakeItEasy;
    using Fakes;
    
    using Nancy.IO;
    using Nancy.Json;
    using Nancy.ModelBinding;
    using Nancy.ModelBinding.DefaultBodyDeserializers;
    using Nancy.ModelBinding.DefaultConverters;
    
    using Nancy.Tests.Unit.ModelBinding.DefaultBodyDeserializers;
    
    using Xunit.Extensions;
    using Xunit;

    public class DefaultBinderFixture
    {
        private readonly IFieldNameConverter passthroughNameConverter;
        private readonly BindingDefaults emptyDefaults;
        private readonly JavaScriptSerializer serializer;
        private readonly BindingContext defaultBindingContext;

        public DefaultBinderFixture()
        {
            this.defaultBindingContext = new BindingContext();

            this.passthroughNameConverter = A.Fake<IFieldNameConverter>();
            A.CallTo(() => this.passthroughNameConverter.Convert(null)).WithAnyArguments()
                .ReturnsLazily(f => (string)f.Arguments[0]);

            this.emptyDefaults = A.Fake<BindingDefaults>();
            A.CallTo(() => this.emptyDefaults.DefaultBodyDeserializers).Returns(new IBodyDeserializer[] { });
            A.CallTo(() => this.emptyDefaults.DefaultTypeConverters).Returns(new ITypeConverter[] { });

            this.serializer = new JavaScriptSerializer();
            this.serializer.RegisterConverters(JsonSettings.Converters);
        }

        [Fact]
        public void Should_throw_if_type_converters_is_null()
        {
            // Given, When
            var result = Record.Exception(() => new DefaultBinder(null, new IBodyDeserializer[] { }, A.Fake<IFieldNameConverter>(), new BindingDefaults()));

            // Then
            result.ShouldBeOfType(typeof(ArgumentNullException));
        }

        [Fact]
        public void Should_throw_if_body_deserializers_is_null()
        {
            // Given, When
            var result = Record.Exception(() => new DefaultBinder(new ITypeConverter[] { }, null, A.Fake<IFieldNameConverter>(), new BindingDefaults()));

            // Then
            result.ShouldBeOfType(typeof(ArgumentNullException));
        }

        [Fact]
        public void Should_throw_if_field_name_converter_is_null()
        {
            // Given, When
            var result = Record.Exception(() => new DefaultBinder(new ITypeConverter[] { }, new IBodyDeserializer[] { }, null, new BindingDefaults()));

            // Then
            result.ShouldBeOfType(typeof(ArgumentNullException));
        }

        [Fact]
        public void Should_throw_if_defaults_is_null()
        {
            // Given, When
            var result = Record.Exception(() => new DefaultBinder(new ITypeConverter[] { }, new IBodyDeserializer[] { }, A.Fake<IFieldNameConverter>(), null));

            // Then
            result.ShouldBeOfType(typeof(ArgumentNullException));
        }

        [Fact]
        public void Should_call_body_deserializer_if_one_matches()
        {
            // Given
            var deserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => deserializer.CanDeserialize(null, A<BindingContext>._)).WithAnyArguments().Returns(true);
            var binder = this.GetBinder(bodyDeserializers: new[] { deserializer });

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            binder.Bind(context, this.GetType(), null, BindingConfig.Default);

            // Then
            A.CallTo(() => deserializer.Deserialize(null, null, null)).WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void Should_not_call_body_deserializer_if_doesnt_match()
        {
            // Given
            var deserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => deserializer.CanDeserialize(null, A<BindingContext>._)).WithAnyArguments().Returns(false);
            var binder = this.GetBinder(bodyDeserializers: new[] { deserializer });

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            binder.Bind(context, this.GetType(), null, BindingConfig.Default);

            // Then
            A.CallTo(() => deserializer.Deserialize(null, null, null)).WithAnyArguments()
                .MustNotHaveHappened();
        }

        [Fact]
        public void Should_pass_request_content_type_to_can_deserialize()
        {
            // Then
            var deserializer = A.Fake<IBodyDeserializer>();
            var binder = this.GetBinder(bodyDeserializers: new[] { deserializer });

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            binder.Bind(context, this.GetType(), null, BindingConfig.Default);

            // Then
            A.CallTo(() => deserializer.CanDeserialize("application/xml", A<BindingContext>._))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void Should_pass_binding_context_to_can_deserialize()
        {
            // Then
            var deserializer = A.Fake<IBodyDeserializer>();
            var binder = this.GetBinder(bodyDeserializers: new[] { deserializer });

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            binder.Bind(context, this.GetType(), null, BindingConfig.Default);

            // Then
            A.CallTo(() => deserializer.CanDeserialize("application/xml", A<BindingContext>.That.Not.IsNull()))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void Should_use_object_from_deserializer_if_one_returned()
        {
            // Given
            var modelObject = new TestModel { StringProperty = "Hello!" };
            var deserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => deserializer.CanDeserialize(null, A<BindingContext>._)).WithAnyArguments().Returns(true);
            A.CallTo(() => deserializer.Deserialize(null, null, null)).WithAnyArguments().Returns(modelObject);
            var binder = this.GetBinder(bodyDeserializers: new[] { deserializer });

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            var result = binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.ShouldBeOfType<TestModel>();
            ((TestModel)result).StringProperty.ShouldEqual("Hello!");
        }

        [Fact]
        public void Should_use_object_from_deserializer_if_one_returned_and_overwrite_when_allowed()
        {
            // Given
            var modelObject = new TestModel { StringPropertyWithDefaultValue = "Hello!" };
            var deserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => deserializer.CanDeserialize(null, A<BindingContext>._)).WithAnyArguments().Returns(true);
            A.CallTo(() => deserializer.Deserialize(null, null, null)).WithAnyArguments().Returns(modelObject);
            var binder = this.GetBinder(bodyDeserializers: new[] { deserializer });

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            var result = binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.ShouldBeOfType<TestModel>();
            ((TestModel)result).StringPropertyWithDefaultValue.ShouldEqual("Hello!");
        }

        [Fact]
        public void Should_use_object_from_deserializer_if_one_returned_and_not_overwrite_when_not_allowed()
        {
            // Given
            var modelObject = new TestModel { StringPropertyWithDefaultValue = "Hello!" };
            var deserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => deserializer.CanDeserialize(null, A<BindingContext>._)).WithAnyArguments().Returns(true);
            A.CallTo(() => deserializer.Deserialize(null, null, null)).WithAnyArguments().Returns(modelObject);
            var binder = this.GetBinder(bodyDeserializers: new[] { deserializer });

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            var result = binder.Bind(context, typeof(TestModel), null, BindingConfig.NoOverwrite);

            // Then
            result.ShouldBeOfType<TestModel>();
            ((TestModel)result).StringPropertyWithDefaultValue.ShouldEqual("Default Value");
        }

        [Fact]
        public void Should_see_if_a_type_converter_is_available_for_each_property_on_the_model_where_incoming_value_exists()
        {
            // Given
            var typeConverter = A.Fake<ITypeConverter>();
            A.CallTo(() => typeConverter.CanConvertTo(null, null)).WithAnyArguments().Returns(false);
            var binder = this.GetBinder(typeConverters: new[] { typeConverter });
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["StringProperty"] = "Test";
            context.Request.Form["IntProperty"] = "12";

            // When
            binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            A.CallTo(() => typeConverter.CanConvertTo(null, null)).WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Times(2));
        }

        [Fact]
        public void Should_call_convert_on_type_converter_if_available()
        {
            // Given
            var typeConverter = A.Fake<ITypeConverter>();
            A.CallTo(() => typeConverter.CanConvertTo(typeof(string), null)).WithAnyArguments().Returns(true);
            A.CallTo(() => typeConverter.Convert(null, null, null)).WithAnyArguments().Returns(null);
            var binder = this.GetBinder(typeConverters: new[] { typeConverter });
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["StringProperty"] = "Test";

            // When
            binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            A.CallTo(() => typeConverter.Convert(null, null, null)).WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void Should_ignore_properties_that_cannot_be_converted()
        {
            // Given
            var binder = this.GetBinder(typeConverters: new[] { new FallbackConverter() });
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["StringProperty"] = "Test";
            context.Request.Form["IntProperty"] = "12";
            context.Request.Form["DateProperty"] = "Broken";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(12);
            result.DateProperty.ShouldEqual(default(DateTime));
        }

        [Fact]
        public void Should_throw_ModelBindingException_if_convertion_of_a_property_fails()
        {
            // Given
            var binder = this.GetBinder(typeConverters: new[] { new FallbackConverter() });
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["IntProperty"] = "badint";
            context.Request.Form["AnotherIntProperty"] = "morebad";

            // Then
            Assert.Throws<ModelBindingException>(() => binder.Bind(context, typeof(TestModel), null, BindingConfig.Default))
                .ShouldMatch(exception =>
                             exception.BoundType == typeof(TestModel)
                             && exception.PropertyBindingExceptions.Any(pe =>
                                                                        pe.PropertyName == "IntProperty"
                                                                        && pe.AttemptedValue == "badint"
                                                                        && pe.InnerException.Message == "badint is not a valid value for Int32.")
                             && exception.PropertyBindingExceptions.Any(pe =>
                                                                        pe.PropertyName == "AnotherIntProperty"
                                                                        && pe.AttemptedValue == "morebad"
                                                                        && pe.InnerException.Message == "morebad is not a valid value for Int32."));
        }

        [Fact]
        public void Should_ignore_indexer_properties()
        {
            // Given
            var binder = this.GetBinder(typeConverters: new[] { new FallbackConverter() });
            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            var validProperties = 0;
            var deserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => deserializer.CanDeserialize(A<string>.Ignored, A<BindingContext>._)).Returns(true);
            A.CallTo(() => deserializer.Deserialize(A<string>.Ignored, A<Stream>.Ignored, A<BindingContext>.Ignored))
                                       .Invokes(f =>
                                           {
                                               validProperties = f.Arguments.Get<BindingContext>(2).ValidModelProperties.Count();
                                           })
                                       .Returns(new TestModel());

            A.CallTo(() => this.emptyDefaults.DefaultBodyDeserializers).Returns(new[] { deserializer });

            // When
            binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            validProperties.ShouldEqual(9);
        }

        [Fact]
        public void Should_pass_binding_context_to_default_deserializer()
        {
            // Given
            var deserializer = A.Fake<IBodyDeserializer>();
            var binder = this.GetBinder();

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            A.CallTo(() => this.emptyDefaults.DefaultBodyDeserializers).Returns(new[] { deserializer });

            // When
            binder.Bind(context, this.GetType(), null, BindingConfig.Default);

            // Then
            A.CallTo(() => deserializer.CanDeserialize("application/xml", A<BindingContext>.That.Not.IsNull()))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void Should_use_field_name_converter_for_each_field()
        {
            // Given
            var binder = this.GetBinder();
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["StringProperty"] = "Test";
            context.Request.Form["IntProperty"] = "12";

            // When
            binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            A.CallTo(() => this.passthroughNameConverter.Convert(null)).WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Times(2));
        }

        [Fact]
        public void Should_not_bind_anything_on_blacklist()
        {
            // Given
            var binder = this.GetBinder(typeConverters: new[] { new FallbackConverter() });
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["StringProperty"] = "Test";
            context.Request.Form["IntProperty"] = "12";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default, "IntProperty");

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(0);
        }

        [Fact]
        public void Should_not_bind_anything_on_blacklist_when_the_blacklist_is_specified_by_expressions()
        {
            // Given
            var binder = this.GetBinder(typeConverters: new[] { new FallbackConverter() });
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["StringProperty"] = "Test";
            context.Request.Form["IntProperty"] = "12";
            
            var fakeModule = A.Fake<INancyModule>();
            var fakeModelBinderLocator = A.Fake<IModelBinderLocator>();
            A.CallTo(() => fakeModule.Context).Returns(context);
            A.CallTo(() => fakeModule.ModelBinderLocator).Returns(fakeModelBinderLocator);
            A.CallTo(() => fakeModelBinderLocator.GetBinderForType(typeof (TestModel), context)).Returns(binder);

            // When
            var result = fakeModule.Bind<TestModel>(tm => tm.IntProperty);

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(0);
        }

        [Fact]
        public void Should_use_default_body_deserializer_if_one_found()
        {
            // Given
            var deserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => deserializer.CanDeserialize(null, A<BindingContext>._)).WithAnyArguments().Returns(true);
            A.CallTo(() => this.emptyDefaults.DefaultBodyDeserializers).Returns(new[] { deserializer });
            var binder = this.GetBinder();

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            binder.Bind(context, this.GetType(), null, BindingConfig.Default);

            // Then
            A.CallTo(() => deserializer.Deserialize(null, null, null)).WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void Should_use_default_type_converter_if_one_found()
        {
            // Given
            var typeConverter = A.Fake<ITypeConverter>();
            A.CallTo(() => typeConverter.CanConvertTo(typeof(string), null)).WithAnyArguments().Returns(true);
            A.CallTo(() => typeConverter.Convert(null, null, null)).WithAnyArguments().Returns(null);
            A.CallTo(() => this.emptyDefaults.DefaultTypeConverters).Returns(new[] { typeConverter });
            var binder = this.GetBinder(new ITypeConverter[] { });
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["StringProperty"] = "Test";

            // When
            binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            A.CallTo(() => typeConverter.Convert(null, null, null)).WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void User_body_serializer_should_take_precedence_over_default_one()
        {
            // Given
            var userDeserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => userDeserializer.CanDeserialize(null, A<BindingContext>._)).WithAnyArguments().Returns(true);

            var defaultDeserializer = A.Fake<IBodyDeserializer>();
            A.CallTo(() => defaultDeserializer.CanDeserialize(null, A<BindingContext>._)).WithAnyArguments().Returns(true);

            A.CallTo(() => this.emptyDefaults.DefaultBodyDeserializers).Returns(new[] { defaultDeserializer });
            var binder = this.GetBinder(bodyDeserializers: new[] { userDeserializer });

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            binder.Bind(context, this.GetType(), null, BindingConfig.Default);

            // Then
            A.CallTo(() => userDeserializer.Deserialize(null, null, null)).WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => defaultDeserializer.Deserialize(null, null, null)).WithAnyArguments()
                .MustNotHaveHappened();
        }

        [Fact]
        public void User_type_converter_should_take_precedence_over_default_one()
        {
            // Given
            var userTypeConverter = A.Fake<ITypeConverter>();
            A.CallTo(() => userTypeConverter.CanConvertTo(typeof(string), null)).WithAnyArguments().Returns(true);
            A.CallTo(() => userTypeConverter.Convert(null, null, null)).WithAnyArguments().Returns(null);
            var defaultTypeConverter = A.Fake<ITypeConverter>();
            A.CallTo(() => defaultTypeConverter.CanConvertTo(typeof(string), null)).WithAnyArguments().Returns(true);
            A.CallTo(() => defaultTypeConverter.Convert(null, null, null)).WithAnyArguments().Returns(null);
            A.CallTo(() => this.emptyDefaults.DefaultTypeConverters).Returns(new[] { defaultTypeConverter });
            var binder = this.GetBinder(new[] { userTypeConverter });
            var context = new NancyContext { Request = new FakeRequest("GET", "/") };
            context.Request.Form["StringProperty"] = "Test";

            // When
            binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            A.CallTo(() => userTypeConverter.Convert(null, null, null)).WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => defaultTypeConverter.Convert(null, null, null)).WithAnyArguments()
                .MustNotHaveHappened();
        }

        [Fact]
        public void Should_bind_model_from_request()
        {
            // Given
            var binder = this.GetBinder();
            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Request.Query["StringProperty"] = "Test";
            context.Request.Query["IntProperty"] = "3";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(3);
        }

        [Fact]
        public void Should_bind_model_from_context_parameters()
        {
            // Given
            var binder = this.GetBinder();
            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Parameters["StringProperty"] = "Test";
            context.Parameters["IntProperty"] = "3";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(3);
        }

        [Fact]
        public void Form_properties_should_take_precendence_over_request_properties()
        {
            // Given
            var binder = this.GetBinder();
            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Request.Form["StringProperty"] = "Test";
            context.Request.Form["IntProperty"] = "3";
            context.Request.Query["StringProperty"] = "Test2";
            context.Request.Query["IntProperty"] = "1";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(3);
        }

        [Fact]
        public void Should_bind_multiple_Form_properties_to_list()
        {
            //Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var binder = this.GetBinder(typeConverters);

            var context = CreateContextWithHeader("Content-Type", new[] { "application/x-www-form-urlencoded" });
            context.Request.Form["StringProperty_0"] = "Test";
            context.Request.Form["IntProperty_0"] = "1";
            context.Request.Form["StringProperty_1"] = "Test2";
            context.Request.Form["IntProperty_1"] = "2";

            // When
            var result = (List<TestModel>)binder.Bind(context, typeof(List<TestModel>), null, BindingConfig.Default);

            // Then
            result.First().StringProperty.ShouldEqual("Test");
            result.First().IntProperty.ShouldEqual(1);
            result.Last().StringProperty.ShouldEqual("Test2");
            result.Last().IntProperty.ShouldEqual(2);
        }

        [Fact]
        public void Should_bind_to_IEnumerable_from_Form()
        {
            //Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var binder = this.GetBinder(typeConverters);

            var context = CreateContextWithHeader("Content-Type", new[] { "application/x-www-form-urlencoded" });

            context.Request.Form["IntValues"] = "1,2,3,4";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.IntValues.ShouldHaveCount(4);
        }

        [Fact]
        public void Should_bind_to_IEnumerable_from_Form_with_multiple_inputs()
        {
            // Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var binder = this.GetBinder(typeConverters);

            var context = CreateContextWithHeader("Content-Type", new[] { "application/x-www-form-urlencoded" });

            context.Request.Form["IntValues_0"] = "1,2,3,4";
            context.Request.Form["IntValues_1"] = "5,6,7,8";

            // When
            var result = (List<TestModel>)binder.Bind(context, typeof(List<TestModel>), null, BindingConfig.Default);
            
            // Then
            result.First().IntValues.ShouldHaveCount(4);
            result.First().IntValues.ShouldEqualSequence(new[] { 1, 2, 3, 4 });
            result.Last().IntValues.ShouldHaveCount(4);
            result.Last().IntValues.ShouldEqualSequence(new[] { 5, 6, 7, 8 });
        }


        [Fact]
        public void Should_bind_to_IEnumerable_from_Form_with_multiple_inputs_using_brackets_and_specifying_an_instance()
        {
            // Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var binder = this.GetBinder(typeConverters);

            var context = CreateContextWithHeader("Content-Type", new[] { "application/x-www-form-urlencoded" });

            context.Request.Form["IntValues[0]"] = "1,2,3,4";
            context.Request.Form["IntValues[1]"] = "5,6,7,8";

            // When
            var result = (List<TestModel>)binder.Bind(context, typeof(List<TestModel>), new List<TestModel> { new TestModel {AnotherStringProprety = "Test"} }, new BindingConfig { Overwrite = false});

            // Then
            result.First().AnotherStringProprety.ShouldEqual("Test");
            result.First().IntValues.ShouldHaveCount(4);
            result.First().IntValues.ShouldEqualSequence(new[] { 1, 2, 3, 4 });
            result.Last().IntValues.ShouldHaveCount(4);
            result.Last().IntValues.ShouldEqualSequence(new[] { 5, 6, 7, 8 });
        }

        [Fact]
        public void Should_bind_to_IEnumerable_from_Form_with_multiple_inputs_using_brackets()
        {
            // Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var binder = this.GetBinder(typeConverters);

            var context = CreateContextWithHeader("Content-Type", new[] { "application/x-www-form-urlencoded" });

            context.Request.Form["IntValues[0]"] = "1,2,3,4";
            context.Request.Form["IntValues[1]"] = "5,6,7,8";

            // When
            var result = (List<TestModel>)binder.Bind(context, typeof(List<TestModel>), null, BindingConfig.Default);

            // Then
            result.First().IntValues.ShouldHaveCount(4);
            result.First().IntValues.ShouldEqualSequence(new[] { 1, 2, 3, 4 });
            result.Last().IntValues.ShouldHaveCount(4);
            result.Last().IntValues.ShouldEqualSequence(new[] { 5, 6, 7, 8 });
        }

        [Fact]
        public void Form_properties_should_take_precendence_over_request_properties_and_context_properties()
        {
            // Given
            var binder = this.GetBinder();

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Request.Form["StringProperty"] = "Test";
            context.Request.Form["IntProperty"] = "3";
            context.Request.Query["StringProperty"] = "Test2";
            context.Request.Query["IntProperty"] = "1";
            context.Parameters["StringProperty"] = "Test3";
            context.Parameters["IntProperty"] = "2";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);
            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(3);
        }

        [Fact]
        public void Request_properties_should_take_precendence_over_context_properties()
        {
            // Given
            var binder = this.GetBinder();

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            context.Request.Query["StringProperty"] = "Test";
            context.Request.Query["IntProperty"] = "12";
            context.Parameters["StringProperty"] = "Test2";
            context.Parameters["IntProperty"] = "13";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);
            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(12);
        }

        [Fact]
        public void Should_be_able_to_bind_from_form_and_request_simultaneously()
        {
            // Given
            var binder = this.GetBinder();

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Request.Form["StringProperty"] = "Test";
            context.Request.Query["IntProperty"] = "12";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(12);
        }

        [Theory]
        [InlineData("de-DE", 4.50)]
        [InlineData("en-GB", 450)]
        [InlineData("en-US", 450)]
        [InlineData("sv-SE", 4.50)]
        [InlineData("ru-RU", 4.50)]
        [InlineData("zh-TW", 450)]
        public void Should_be_able_to_bind_culturally_aware_form_properties_if_numeric(string culture, double expected)
        {
            // Given
            var binder = this.GetBinder();

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Culture = new CultureInfo(culture);
            context.Request.Form["DoubleProperty"] = "4,50";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.DoubleProperty.ShouldEqual(expected);
        }

        [Theory]
        [InlineData("12/25/2012", 12, 25, 2012, "en-US")]
        [InlineData("12/12/2012", 12, 12, 2012, "en-US")]
        [InlineData("25/12/2012", 12, 25, 2012, "en-GB")]
        [InlineData("12/12/2012", 12, 12, 2012, "en-GB")]
        [InlineData("12/12/2012", 12, 12, 2012, "ru-RU")]
        [InlineData("25/12/2012", 12, 25, 2012, "ru-RU")]
        [InlineData("2012-12-25", 12, 25, 2012, "zh-TW")]
        [InlineData("2012-12-12", 12, 12, 2012, "zh-TW")]
        public void Should_be_able_to_bind_culturally_aware_form_properties_if_datetime(string date, int month, int day, int year, string culture)
        {
            // Given
            var binder = this.GetBinder();

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Culture = new CultureInfo(culture);
            context.Request.Form["DateProperty"] = date;

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.DateProperty.Date.Month.ShouldEqual(month);
            result.DateProperty.Date.Day.ShouldEqual(day);
            result.DateProperty.Date.Year.ShouldEqual(year);
        }



        [Fact]
        public void Should_be_able_to_bind_from_request_and_context_simultaneously()
        {
            // Given
            var binder = this.GetBinder();

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Request.Query["StringProperty"] = "Test";
            context.Parameters["IntProperty"] = "12";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(12);
        }

        [Fact]
        public void Should_not_overwrite_nullable_property_if_already_set_and_overwriting_is_not_allowed()
        {
            // Given
            var binder = this.GetBinder();
            var existing = new TestModel { StringProperty = "Existing Value" };

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            context.Request.Query["StringProperty"] = "Test";
            context.Request.Query["IntProperty"] = "12";
            context.Parameters["StringProperty"] = "Test2";
            context.Parameters["IntProperty"] = "1";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), existing, BindingConfig.NoOverwrite);

            // Then
            result.StringProperty.ShouldEqual("Existing Value");
            result.IntProperty.ShouldEqual(12);
        }

        [Fact]
        public void Should_not_overwrite_non_nullable_property_if_already_set_and_overwriting_is_not_allowed()
        {
            // Given
            var binder = this.GetBinder();
            var existing = new TestModel { IntProperty = 27 };

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            context.Request.Query["StringProperty"] = "Test";
            context.Request.Query["IntProperty"] = "12";
            context.Parameters["StringProperty"] = "Test2";
            context.Parameters["IntProperty"] = "1";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), existing, BindingConfig.NoOverwrite);

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.IntProperty.ShouldEqual(27);
        }

        [Fact]
        public void Should_overwrite_nullable_property_if_already_set_and_overwriting_is_allowed()
        {
            // Given
            var binder = this.GetBinder();
            var existing = new TestModel { StringProperty = "Existing Value" };

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            context.Parameters["StringProperty"] = "Test2";
            context.Parameters["IntProperty"] = "1";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), existing, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("Test2");
            result.IntProperty.ShouldEqual(1);
        }

        [Fact]
        public void Should_overwrite_non_nullable_property_if_already_set_and_overwriting_is_allowed()
        {
            // Given
            var binder = this.GetBinder();
            var existing = new TestModel { IntProperty = 27 };

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            context.Parameters["StringProperty"] = "Test2";
            context.Parameters["IntProperty"] = "1";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), existing, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("Test2");
            result.IntProperty.ShouldEqual(1);
        }

        [Fact]
        public void Should_bind_list_model_from_body()
        {
            //Given
            var binder = this.GetBinder(null, new List<IBodyDeserializer> { new XmlBodyDeserializer() });
            var body = XmlBodyDeserializerFixture.ToXmlString(new List<TestModel>(new[] { new TestModel { StringProperty = "Test" }, new TestModel { StringProperty = "AnotherTest" } }));

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/xml" }, body);

            // When
            var result = (List<TestModel>)binder.Bind(context, typeof(List<TestModel>), null, BindingConfig.Default);

            // Then
            result.First().StringProperty.ShouldEqual("Test");
            result.Last().StringProperty.ShouldEqual("AnotherTest");
        }


        [Fact]
        public void Should_bind_array_model_from_body()
        {
            //Given
            var binder = this.GetBinder(null, new List<IBodyDeserializer> { new XmlBodyDeserializer() });
            var body = XmlBodyDeserializerFixture.ToXmlString(new List<TestModel>(new[] { new TestModel { StringProperty = "Test" }, new TestModel { StringProperty = "AnotherTest" } }));

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/xml" }, body);

            // When
            var result = (TestModel[])binder.Bind(context, typeof(TestModel[]), null, BindingConfig.Default);
            
            // Then
            result.First().StringProperty.ShouldEqual("Test");
            result.Last().StringProperty.ShouldEqual("AnotherTest");
        }

        [Fact]
        public void Should_bind_ienumerable_model_from_body()
        {
            //Given
            var binder = this.GetBinder(null, new List<IBodyDeserializer> { new JsonBodyDeserializer() });
            var body = serializer.Serialize(new List<TestModel>(new[] { new TestModel { StringProperty = "Test" }, new TestModel { StringProperty = "AnotherTest" } }));

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/json" }, body);

            // When
            var result = (IEnumerable<TestModel>)binder.Bind(context, typeof(IEnumerable<TestModel>), null, BindingConfig.Default);

            // Then
            result.First().StringProperty.ShouldEqual("Test");
            result.Last().StringProperty.ShouldEqual("AnotherTest");
        }


        [Fact]
        public void Should_bind_ienumerable_model_with_instance_from_body()
        {
            //Given
            var binder = this.GetBinder(null, new List<IBodyDeserializer> { new JsonBodyDeserializer() });
            var body = serializer.Serialize(new List<TestModel>(new[] { new TestModel { StringProperty = "Test" }, new TestModel { StringProperty = "AnotherTest" } }));

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/json" }, body);

            var then = DateTime.Now;
            var instance = new List<TestModel> { new TestModel{ DateProperty = then }, new TestModel { IntProperty = 9, AnotherStringProprety = "Bananas" } };

            // When
            var result = (IEnumerable<TestModel>)binder.Bind(context, typeof(IEnumerable<TestModel>), instance, new BindingConfig{Overwrite = false});

            // Then
            result.First().StringProperty.ShouldEqual("Test");
            result.First().DateProperty.ShouldEqual(then);
            result.Last().StringProperty.ShouldEqual("AnotherTest");
            result.Last().IntProperty.ShouldEqual(9);
            result.Last().AnotherStringProprety.ShouldEqual("Bananas");
        }
        
        [Fact]
        public void Should_bind_model_with_instance_from_body()
        {
            //Given
            var binder = this.GetBinder(null, new List<IBodyDeserializer> { new XmlBodyDeserializer() });
            var body = XmlBodyDeserializerFixture.ToXmlString(new TestModel { StringProperty = "Test" });

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/xml" }, body);

            var then = DateTime.Now;
            var instance = new TestModel { DateProperty = then, IntProperty = 6, AnotherStringProprety = "Beers" };

            // Wham
            var result = (TestModel)binder.Bind(context, typeof(TestModel), instance, new BindingConfig { Overwrite = false });

            // Then
            result.StringProperty.ShouldEqual("Test");
            result.DateProperty.ShouldEqual(then);
            result.IntProperty.ShouldEqual(6);
            result.AnotherStringProprety.ShouldEqual("Beers");
        }
        
        [Fact]
        public void Should_bind_model_from_body_that_contains_an_array()
        {
            //Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var binder = this.GetBinder(typeConverters, new List<IBodyDeserializer> { new JsonBodyDeserializer() });
            var body = serializer.Serialize(new TestModel {StringProperty = "Test", SomeStrings = new[] {"E", "A", "D", "G", "B", "E"}});
            
            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/json" }, body);

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.SomeStrings.ShouldHaveCount(6);
            result.SomeStrings.ShouldEqualSequence(new[] { "E", "A", "D", "G", "B", "E" });
        }


        [Fact]
        public void Should_bind_array_model_from_body_that_contains_an_array()
        {
            //Given
            var binder = this.GetBinder(null, new List<IBodyDeserializer> { new JsonBodyDeserializer() });
            var body =
                serializer.Serialize(new[]
                {
                    new TestModel {StringProperty = "Test", SomeStrings = new[] {"E", "A", "D", "G", "B", "E"}},
                    new TestModel {StringProperty = "AnotherTest", SomeStrings = new[] {"E", "A", "D", "G", "B", "E"}}
                });

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/json" }, body);

            // When
            var result = (TestModel[])binder.Bind(context, typeof(TestModel[]), null, BindingConfig.Default, "SomeStrings");
            
            // Then
            result.First().SomeStrings.ShouldBeNull();
            result.Last().SomeStrings.ShouldBeNull();
        }


        [Fact]
        public void Form_request_and_context_properties_should_take_precedence_over_body_properties()
        {
            // Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var bodyDeserializers = new IBodyDeserializer[] { new XmlBodyDeserializer() };
            var binder = this.GetBinder(typeConverters, bodyDeserializers);
            var body = XmlBodyDeserializerFixture.ToXmlString(new TestModel { IntProperty = 0, StringProperty = "From body" });

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/xml" }, body);

            context.Request.Form["StringProperty"] = "From form";
            context.Request.Query["IntProperty"] = "1";
            context.Parameters["AnotherStringProprety"] = "From context";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("From form");
            result.AnotherStringProprety.ShouldEqual("From context");
            result.IntProperty.ShouldEqual(1);
        }

        [Fact]
        public void Form_request_and_context_properties_should_be_ignored_in_body_only_mode_when_there_is_a_body()
        {
            // Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var bodyDeserializers = new IBodyDeserializer[] { new XmlBodyDeserializer() };
            var binder = GetBinder(typeConverters, bodyDeserializers);
            var body = XmlBodyDeserializerFixture.ToXmlString(new TestModel { IntProperty = 2, StringProperty = "From body" });

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/xml" }, body);

            context.Request.Form["StringProperty"] = "From form";
            context.Request.Query["IntProperty"] = "1";
            context.Parameters["AnotherStringProprety"] = "From context";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, new BindingConfig { BodyOnly = true });

            // Then
            result.StringProperty.ShouldEqual("From body");
            result.AnotherStringProprety.ShouldBeNull(); // not in body, so default value
            result.IntProperty.ShouldEqual(2);
        }

        [Fact]
        public void Form_request_and_context_properties_should_NOT_be_used_in_body_only_mode_if_there_is_no_body()
        {
            // Given
            var typeConverters = new ITypeConverter[] { new CollectionConverter(), new FallbackConverter() };
            var binder = GetBinder(typeConverters);

            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });
            context.Request.Form["StringProperty"] = "From form";
            context.Request.Query["IntProperty"] = "1";
            context.Parameters["AnotherStringProprety"] = "From context";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, new BindingConfig { BodyOnly = true });

            // Then
            result.StringProperty.ShouldEqual(null);
            result.AnotherStringProprety.ShouldEqual(null);
            result.IntProperty.ShouldEqual(0);
        }


        [Fact]
        public void Should_be_able_to_bind_body_request_form_and_context_properties()
        {
            //Given 
            var binder = this.GetBinder(null, new List<IBodyDeserializer> { new XmlBodyDeserializer() });
            var body = XmlBodyDeserializerFixture.ToXmlString(new TestModel { DateProperty = new DateTime(2012, 8, 16) });

            var context = CreateContextWithHeaderAndBody("Content-Type", new[] { "application/xml" }, body);

            context.Request.Form["IntProperty"] = "0";
            context.Request.Query["StringProperty"] = "From Query";
            context.Parameters["AnotherStringProprety"] = "From Context";

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), null, BindingConfig.Default);

            // Then
            result.StringProperty.ShouldEqual("From Query");
            result.IntProperty.ShouldEqual(0);
            result.DateProperty.ShouldEqual(new DateTime(2012, 8, 16));
            result.AnotherStringProprety.ShouldEqual("From Context");
        }

        [Fact]
        public void Should_ignore_existing_instance_if_type_doesnt_match()
        {
            //Given 
            var binder = this.GetBinder();
            var existing = new object();
            var context = CreateContextWithHeader("Content-Type", new[] { "application/xml" });

            // When
            var result = (TestModel)binder.Bind(context, typeof(TestModel), existing, BindingConfig.Default);

            // Then
            result.ShouldNotBeSameAs(existing);
        }

        private IBinder GetBinder(IEnumerable<ITypeConverter> typeConverters = null, IEnumerable<IBodyDeserializer> bodyDeserializers = null, IFieldNameConverter nameConverter = null, BindingDefaults bindingDefaults = null)
        {
            var converters = typeConverters ?? new ITypeConverter[] { new DateTimeConverter(), new NumericConverter(), new FallbackConverter() };
            var deserializers = bodyDeserializers ?? new IBodyDeserializer[] { };
            var converter = nameConverter ?? this.passthroughNameConverter;
            var defaults = bindingDefaults ?? this.emptyDefaults;

            return new DefaultBinder(converters, deserializers, converter, defaults);
        }

        private static NancyContext CreateContextWithHeader(string name, IEnumerable<string> values)
        {
            var header = new Dictionary<string, IEnumerable<string>>
            {
                { name, values }
            };

            return new NancyContext
            {
                Request = new FakeRequest("GET", "/", header),
                Parameters = DynamicDictionary.Empty
            };
        }

        private static NancyContext CreateContextWithHeaderAndBody(string name, IEnumerable<string> values, string body)
        {
            var header = new Dictionary<string, IEnumerable<string>>
            {
                { name, values }
            };

            byte[] byteArray = Encoding.UTF8.GetBytes(body);
            var bodyStream = RequestStream.FromStream(new MemoryStream(byteArray));

            return new NancyContext
            {
                Request = new FakeRequest("GET", "/", header, bodyStream, "http", string.Empty),
                Parameters = DynamicDictionary.Empty
            };
        }

        public class TestModel
        {
            public TestModel()
            {
                this.StringPropertyWithDefaultValue = "Default Value";
            }

            public string StringProperty { get; set; }

            public string AnotherStringProprety { get; set; }

            public int IntProperty { get; set; }

            public int AnotherIntProperty { get; set; }

            public DateTime DateProperty { get; set; }

            public string StringPropertyWithDefaultValue { get; set; }

            public double DoubleProperty { get; set; }

            [XmlIgnore]
            public IEnumerable<int> IntValues { get; set; }

            public string[] SomeStrings { get; set; }
            
            public int this[int index]
            {
                get { return 0; }
                set { }
            }
        }
    }

    public class BindingConfigFixture
    {
        [Fact]
        public void Should_allow_overwrite_on_new_instance()
        {
            // Given
            // When
            var instance = new BindingConfig();

            // Then
            instance.Overwrite.ShouldBeTrue();
        }
    }
}
