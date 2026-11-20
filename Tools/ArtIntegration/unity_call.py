"""Small stdlib client for the project's existing local Unity-MCP bridge."""
import json
import pathlib
import sys
import urllib.request


def call(tool, arguments):
    url = json.loads(pathlib.Path('.mcp.json').read_text())['mcpServers']['ai-game-developer']['url']
    headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}

    def request(payload):
        with urllib.request.urlopen(urllib.request.Request(url, data=json.dumps(payload).encode(), headers=headers), timeout=300) as response:
            body = response.read().decode()
            session = response.headers.get('Mcp-Session-Id')
            if session:
                headers['Mcp-Session-Id'] = session
            if body.startswith('{'):
                return json.loads(body)
            return next((json.loads(line[6:]) for line in body.splitlines() if line.startswith('data: ')), {})

    request({'jsonrpc': '2.0', 'id': 1, 'method': 'initialize', 'params': {'protocolVersion': '2024-11-05', 'capabilities': {}, 'clientInfo': {'name': 'codex', 'version': '1'}}})
    request({'jsonrpc': '2.0', 'method': 'notifications/initialized'})
    return request({'jsonrpc': '2.0', 'id': 2, 'method': 'tools/call', 'params': {'name': tool, 'arguments': arguments}})


if __name__ == '__main__':
    args = json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig'))
    result = call(sys.argv[1], args)
    out = pathlib.Path('Tools/ArtIntegration/.runtime/last-result.json')
    out.write_text(json.dumps(result), encoding='utf-8')
    for item in result.get('result', {}).get('content', []):
        if item.get('type') == 'text':
            print(item['text'][:18000])
    if 'error' in result:
        print(json.dumps(result['error']))
