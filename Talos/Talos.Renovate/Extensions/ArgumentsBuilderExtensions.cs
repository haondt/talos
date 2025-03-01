using CliWrap.Builders;

namespace Talos.Renovate.Extensions
{
    public static class ArgumentsBuilderExtensions
    {
        public static ArgumentsBuilder AddIf(this ArgumentsBuilder builder, bool condition, string parameter)
        {
            if (condition)
                return builder.Add(parameter);
            return builder;
        }
        public static ArgumentsBuilder AddIf(this ArgumentsBuilder builder, bool condition, IEnumerable<string> parameters)
        {
            if (condition)
                return builder.AddRange(parameters);
            return builder;
        }
        public static ArgumentsBuilder AddRange(this ArgumentsBuilder builder, IEnumerable<string> parameters)
        {
            foreach (var parameter in parameters)
                builder = builder.Add(parameter);
            return builder;
        }
    }
}
