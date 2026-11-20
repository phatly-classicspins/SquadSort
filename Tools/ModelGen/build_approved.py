"""Run the project's model-gen door for the three approved source designs."""
from pathlib import Path
import concurrent.futures
import json
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
LOGS = ROOT / 'Tools/ModelGen/.runtime/build-logs'

def build(name):
    request = ROOT / f'Tools/ModelGen/requests/mockup-v1-{name}.json'
    log = LOGS / f'{name}.log'
    with log.open('w', encoding='utf-8') as fh:
        rc = subprocess.run([sys.executable, str(ROOT/'Tools/pf/model-gen.py'), 'build',
            str(request), '--variants', '1', '--rounds', '3'], cwd=ROOT,
            stdout=fh, stderr=subprocess.STDOUT, timeout=2400).returncode
    return {'model':name, 'exit_code':rc, 'log':str(log)}

if __name__ == '__main__':
    LOGS.mkdir(parents=True, exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        jobs = [pool.submit(build,n) for n in ('character-purple','queue-four','waiting-board')]
        for job in concurrent.futures.as_completed(jobs):
            print(json.dumps(job.result()), flush=True)
