namespace Palforge.Commands.Arguments;

internal ref struct RawArgumentReader
{
    private const char Quote = '"';

    private readonly ReadOnlySpan<char> _input;
    private int _position;

    public readonly ReadOnlySpan<char> Remaining =>
        _position >= _input.Length ? default : _input[_position..].TrimStart();

    public RawArgumentReader(ReadOnlySpan<char> input)
    {
        _input = input;
        _position = 0;
    }

    public bool TryReadToken(out ReadOnlySpan<char> token)
    {
        SkipWhitespace();

        if (_position >= _input.Length)
        {
            token = default;
            return false;
        }

        if (_input[_position] == Quote)
        {
            var quoted = ++_position;

            while (_position < _input.Length && _input[_position] != Quote)
                _position++;

            token = _input[quoted.._position];

            if (_position < _input.Length)
                _position++;

            return true;
        }

        var start = _position;

        while (_position < _input.Length && !char.IsWhiteSpace(_input[_position]))
            _position++;

        token = _input[start.._position];

        return true;
    }

    public ReadOnlySpan<char> ReadRemainder()
    {
        SkipWhitespace();

        var rest = _input[_position..];
        _position = _input.Length;

        return rest.TrimEnd();
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            _position++;
    }
}