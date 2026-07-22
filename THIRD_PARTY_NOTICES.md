# Third-party notices

## tldr-pages

The offline command examples in `src/CommandMan.Core/Data/tldr.json` are derived
from the [tldr-pages project](https://github.com/tldr-pages/tldr).

Copyright © 2014—present the tldr-pages team and contributors.

The tldr-pages content is licensed under the Creative Commons Attribution 4.0
International License (CC BY 4.0). A copy of the upstream license is included at
`third_party/tldr/LICENSE.md`. The exact upstream Git commit, source directories,
and imported page counts are recorded in `third_party/tldr/SOURCE.json`.

Command Man converts the upstream Markdown pages into a compact JSON index,
selects the short option from tldr option alternatives for copyable examples,
and prefers Simplified Chinese descriptions when translations are available.
