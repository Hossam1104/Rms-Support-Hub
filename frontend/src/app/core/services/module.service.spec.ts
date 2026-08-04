import { ModuleDto } from '../models';
import { orderModulesForDisplay } from './module.service';

const asModule = (key: string): ModuleDto => ({ key, label: key } as unknown as ModuleDto);

describe('orderModulesForDisplay', () => {
  it('pins UPC to the front whatever order the backend returned', () => {
    const ordered = orderModulesForDisplay([
      asModule('ghc_ecommerce'),
      asModule('ghc_unicommerce'),
      asModule('upc_ecommerce')
    ]);

    expect(ordered.map(m => m.key)).toEqual(['upc_ecommerce', 'ghc_ecommerce', 'ghc_unicommerce']);
  });

  it('keeps the backend order for everything behind the pinned keys', () => {
    const ordered = orderModulesForDisplay([
      asModule('upc_ecommerce'),
      asModule('ghc_unicommerce'),
      asModule('ghc_ecommerce')
    ]);

    expect(ordered.map(m => m.key)).toEqual(['upc_ecommerce', 'ghc_unicommerce', 'ghc_ecommerce']);
  });

  it('does not mutate the input array and tolerates a missing UPC module', () => {
    const input = [asModule('ghc_ecommerce'), asModule('ghc_unicommerce')];

    const ordered = orderModulesForDisplay(input);

    expect(ordered).not.toBe(input);
    expect(input.map(m => m.key)).toEqual(['ghc_ecommerce', 'ghc_unicommerce']);
    expect(ordered.map(m => m.key)).toEqual(['ghc_ecommerce', 'ghc_unicommerce']);
  });

  it('returns an empty list unchanged', () => {
    expect(orderModulesForDisplay([])).toEqual([]);
  });
});
