from utils import *

# Получаем ПД из порезанного файлика
file = "saved/1/1.txt"
time, voltage = open_txt(file)

# Строим график ПД
plt.figure()
plt.plot(time, voltage)
plt.xlabel("Время (мс)")
plt.ylabel("Напряжение (мВ)")
plt.show()