using ClassFabric.Core.Abstractions.Converters;
using ClassFabric.Shared.Enums;

namespace ClassFabric.Converters;

public class AuthLevelToIntConverter : EnumToIntConverter<AuthorizeLevel>;