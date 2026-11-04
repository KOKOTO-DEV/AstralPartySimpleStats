# AstralPartySimpleStats 0.1.0

아스트랄파티 글로벌 Steam INT판의 참가자 통계를 표시하는 독립 BepInEx 플러그인입니다.
ChatPlugin 없이 사용할 수 있습니다.

```text
승률: 88.26% (203/230) · 좋아요: 218.6 (656)
매너: 100
```

- 승률: 총 승리/총 플레이 × 100, 소수 둘째 자리 반올림
- 좋아요: 전체 좋아요 ÷ 3을 소수 첫째 자리에서 버림, 괄호 안은 전체 좋아요
- 매너: 방 참가자 CreditInfo의 차감점수를 100에서 뺀 값, 범위 0~100
- 비공개 통계는 `비공개`, 전달되지 않은 정보는 `확인 불가`

대기실·캐릭터 선택 화면의 프로필 카드 상단에 살짝 겹쳐 표시합니다.
글자는 왼쪽 정렬이며, 상자 오른쪽 끝을 고정하고 내용 길이에 따라 왼쪽으로 확장합니다.
대기실은 말풍선 버튼을 피하는 오른쪽 여백을 유지하고, 캐릭터 선택창은 카드 왼쪽 끝에 맞춰 오른쪽으로 확장합니다.

게임의 기존 프로필 요청으로 한 번에 한 명씩 최소 2초 간격으로 자동 조회하며 60초 동안 재사용합니다.
프로필 창은 열지 않고, 수동 프로필 창이 열려 있으면 새 요청을 멈춥니다.
요청 객체별로 응답을 구분해서 자동 조회가 수동 프로필 창 데이터를 덮어쓰지 않습니다.
통계 데이터는 별도 채팅 서버로 전송하지 않습니다.

## 설치

Windows x64 글로벌 Steam INT판과 BepInEx 6 Unity IL2CPP가 필요합니다.
게임 종료 후 배포 ZIP의 `BepInEx` 폴더를 INT 게임 폴더에 합칩니다.

```text
8vJXnINT/BepInEx/plugins/AstralPartySimpleStats/AstralPartySimpleStats.dll
```

설치 DLL은 하나이며 조회용 HybridCLR 브리지는 DLL 안에 내장되어 있습니다.
ChatPlugin은 선택 사항입니다. ChatPlugin 0.2.0 원본과 함께 쓸 수 있습니다.
이 대화에서 만든 통계 통합판 ChatPlugin 0.2.1~0.2.5를 함께 쓰는 경우에는
`astral-party.chat.cfg`의 `[PlayerDetails] Enabled = false`로 기존 표시를 꺼 주세요.
통합판을 0.2.0 원본으로 복원해도 됩니다. 두 통계 기능을 동시에 켜지 마세요.

## 설정과 제거

첫 실행 후 `BepInEx/config/astral-party.simple-stats.cfg`가 생성됩니다.
`[General] Enabled = false`로 표시와 새 자동 조회를 끌 수 있습니다.
삭제하려면 게임 종료 후 `AstralPartySimpleStats` 폴더를 제거합니다.

## 빌드와 검증

.NET SDK 6.0.428과 초기화된 게임의 BepInEx 참조가 필요합니다.
실제 게임 번들에서 추출한 raw HybridCLR DLL을 RuntimeRefs로 지정합니다.
필수 파일은 AstralParty.Runtime.dll, UnityEngine.CoreModule.dll, UniTask.dll,
Google.Protobuf.Runtime.dll입니다. BepInEx interop DLL과 다릅니다.

```powershell
./build.ps1 -GameRoot 'D:/SteamLibrary/steamapps/common/Astral Party/8vJXnINT' -RuntimeRefs '추출한 런타임 DLL 폴더'
dotnet run --project tests/StatsTests.csproj -c Release
dotnet run --project tests/profile-bridge/BridgeTests.csproj -c Release
```

출력은 `src/bin/Release/net6.0/AstralPartySimpleStats.dll`입니다.
통계/슬롯 판독 14개와 조회 브리지 7개 테스트가 통과했습니다. 게임 API는 모의 데이터입니다.
원래 통합판의 표시·조회는 사용자 스크린샷에서 확인했으며 독립판의 실게임 동작은 다음 실행에서 확인이 필요합니다.
문제 진단은 `BepInEx/LogOutput.log`의 `AstralPartySimpleStats` 항목을 확인하세요.

## 라이선스

MIT. [maynut02/astral-party-chat-plugin](https://github.com/maynut02/astral-party-chat-plugin)의
커밋 687be4c3b3cfd5e07e75624fbba33eb9232aae2c와 로컬 통계 구현에서 분리했습니다.
원본 저작권 고지는 LICENSE에 유지했습니다. 배포물에는 게임 DLL을 포함하지 않습니다.

