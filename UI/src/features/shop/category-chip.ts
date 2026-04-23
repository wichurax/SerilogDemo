const defaultCategoryChipClassName = 'border-[#64748b33] bg-[#64748b14] text-[#475569]'

const categoryChipClassNames: Record<string, string> = {
  accessories: 'border-[#a46b1c33] bg-[#a46b1c14] text-[#855716]',
  audio: 'border-[#b85c4a33] bg-[#b85c4a14] text-[#934739]',
  electronics: 'border-[#0f766e33] bg-[#0f766e14] text-[#0f5f59]',
  furniture: 'border-[#9a4f3533] bg-[#9a4f3514] text-[#7f412c]',
  gaming: 'border-[#8b3f5433] bg-[#8b3f5414] text-[#6f3243]',
  office: 'border-[#46638f33] bg-[#46638f14] text-[#385075]',
}

export function getCategoryChipClassName(category: string) {
  return categoryChipClassNames[category.trim().toLowerCase()] ?? defaultCategoryChipClassName
}