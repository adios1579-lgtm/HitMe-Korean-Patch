# 배포자 안내

## 빌드

Windows .NET Framework 4.x C# 컴파일러와 Python 3.11, pefile이 필요합니다. 사용자는 이 개발 도구를 설치할 필요가 없습니다.

`build-config.example.json`을 `build-config.local.json`으로 복사하여 승인된 최신 후보의 경로, 읽기 전용 원본 경로, 콘솔 오프닝, 검증된 Noto Sans KR Regular 파일을 지정합니다. 로컬 설정은 공개하지 않습니다. 출력은 이 폴더의 `release/버전/`입니다.

```powershell
py -3.11 -X utf8 build_release.py test
py -3.11 -X utf8 build_release.py full --version 1.0.0
py -3.11 -X utf8 build_release.py update --version 1.0.1 --notes "수정 사항"
```

매 빌드는 DLL을 다시 컴파일하고 시험합니다. 최초 배포 이후 부트스트랩 EXE는 고정하며 자동 교체 대상에 넣지 않습니다. 기존 버전 번호를 재사용하지 않습니다. `baseline.json`과 모든 `history/*.json`은 작은 파일 목록이며 누적 업데이트를 만들기 위해 보존합니다. 과거 어느 버전과라도 다른 파일을 포함하므로 초기 내용으로 되돌리는 경우도 누락하지 않습니다.

원본 게임 PAC를 재귀 수집하지 않습니다. 명시한 후보 EXE/Update/Config, 콘솔 오프닝, 폰트/라이선스, 실행기/업데이트 DLL/설정/설명서만 배포합니다. 저장 디렉터리와 인증 도구는 배포하지 않으며 본편이 제공해야 합니다. 원본 판본 검사는 `assets/base-requirements.json`의 크기·SHA로 합니다. 지원 판본을 늘릴 때 검사를 무력화하지 말고 실제 판본별 근거를 추가하세요.

초기 버전은 설치 ZIP과 현재 전체 inventory를 가진 manifest를 게시합니다. 그 버전보다 이전의 지원 업데이트 설치본이 없으므로 첫 manifest의 files/packages는 비어 있습니다. 후속 버전부터 누적 ZIP이 생성됩니다. ZIP 하나는 2GiB 미만이며 단일 파일은 1700MiB 미만으로 제한합니다. 보통 파일 합64MiB 단위로 묶고, 큰 단일 PAC/영상은 단독 묶음으로 만듭니다.

## 게시

새 태그의 릴리스 초안에 전체 설치 ZIP, `update-manifest.json`, 모든 `hitme-update-*.zip`, `FILE_HASHES.json`을 올립니다. 자산 이름·버전·해시를 확인한 뒤 정식 Latest로 게시합니다. 이미 공개한 파일을 조용히 교체하지 말고 새 버전을 만드세요. 테스트 릴리스를 이 저장소의 Latest로 올리지 않습니다.

공개 latest manifest와 자산을 다시 받아 로컬 해시를 대조합니다. 단순 git push만으로는 업데이트되지 않습니다. 다운로드는 공개 HTTPS이며 배포자 토큰을 실행기·설정·로그·ZIP에 넣지 않습니다. SHA는 손상 검출이며 manifest 별도 전자서명은 없습니다. 저장소 자체가 탈취되는 상황까지 막는 설계는 아닙니다.

## 시험 범위

`TEST_RESULTS.txt`: 합성 파일 업데이트/실패복구/오프라인/사용자수정/경로·ZIP·버전 검사.
`INTEGRATION_TEST_RESULTS.txt`: 합성 HitMe 프로세스의 작업 디렉터리와 실행 중 감지, 실제 폰트 로드·해제 및 레지스트리 값 수 불변.

이는 실제 게임 실행이나 전 장면 검수가 아닙니다. 최종 변경사항의 실게임 확인은 별도로 기록합니다. 새 엔진을 배포할 때 구버전 엔진에서 최신 manifest와 ZIP을 해석하는 호환성을 확인하고, schema/base_version은 필요 없이 바꾸지 않습니다.

## 참고 구현

사용자가 제공한 Canvas3 자동업데이트 구현을 HitMe 전용 경로·허용 목록·앱 ID로 이식했습니다. Canvas3의 게임 데이터·세이브·아이콘 이행 코드·토큰은 포함하지 않습니다.

정책 확인: [GitHub 릴리스 자산 제한](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases), [Noto 폰트 라이선스](https://github.com/google/fonts/blob/main/ofl/notosanskr/OFL.txt).
