"""Summarizes a Unity test results XML.
Usage: test-results.py <results.xml>           failures with their first stack line (scripts/udon-ide/test.sh)
       test-results.py <results.xml> <suite>   every test case, prefixed "unity[<suite>]" (scripts/run-all-tests.sh)"""
import sys, xml.etree.ElementTree as ET

path, suite = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else None)
prefix = 'unity[%s] ' % suite if suite else ''
try:
    r = ET.parse(path).getroot()
except Exception as e:
    if not suite: raise
    print(prefix + 'no results:', e)
    sys.exit(0)
print(prefix + str(r.attrib.get('result')), 'passed', r.attrib.get('passed'), 'failed', r.attrib.get('failed'))
for tc in r.iter('test-case'):
    if suite:
        f = tc.find('failure')
        print(tc.attrib['result'], tc.attrib['fullname'], (f.findtext('message') or '')[:600] if f is not None else '')
    elif tc.attrib['result'] != 'Passed':
        m = tc.find('failure/message'); st = tc.find('failure/stack-trace')
        print('FAIL', tc.attrib['name'], (m.text or '').strip()[:400] if m is not None else '')
        if st is not None: print('   ', (st.text or '').strip().split('\n')[0][:200])
