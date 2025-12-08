using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wiaoj.Ddd.Abstractions.ValueObjects;
using Wiaoj.Ddd.EntityFrameworkCore.ValueConverters;

namespace Wiaoj.Ddd.EntityFrameworkCore.Extensions;  
public static class ModelBuilderExtensions {
    extension(ModelBuilder modelBuilder) {
        /// <summary>
        /// Scans all entities in the model and automatically applies ValueConverters for:
        /// 1. Properties of type <see cref="RowVersion"/>
        /// 2. Properties implementing <see cref="IId{TSelf, TValue}"/> (Strongly-typed IDs)
        /// </summary>
        public ModelBuilder ApplyDddConventions() {

            IEnumerable<IMutableProperty> properties = modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties());

            foreach (IMutableProperty property in properties) {
                Type type = property.ClrType;

                // 1. RowVersion Converter
                if (type == typeof(RowVersion)) {
                    property.SetValueConverter(typeof(RowVersionConverter));
                    property.IsConcurrencyToken = true;
                    continue;
                }

                // 2. Strongly Typed ID Converter
                // Type "IId<,>" 
                Type? idInterface = type.GetInterfaces()
                    .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IId<,>));

                if (idInterface is not null) {
                    // IId<TId, TValue> 
                    Type idType = idInterface.GetGenericArguments()[0];   // TId
                    Type valueType = idInterface.GetGenericArguments()[1]; // TValue

                    Type converterType = typeof(StronglyTypedIdValueConverter<,>)
                        .MakeGenericType(idType, valueType);

                    property.SetValueConverter(converterType);
                }
            }

            return modelBuilder;
        }
    }
}